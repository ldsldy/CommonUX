using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using InputApi = UnityEngine.InputSystem.InputSystem;

namespace CommonUX.InputSystem
{
    /// <summary>UI 안내 표시에 사용하는 입력 장치 분류입니다. 실제 입력 라우팅을 변경하지 않습니다.</summary>
    public enum UiInputKind { KeyboardAndMouse, Gamepad, Touch }

    /// <summary>스틱 노이즈와 짧은 장치 왕복 입력을 거르는 임계값입니다.</summary>
    public sealed class InputDeviceTrackerOptions
    {
        public float StickActivationThreshold { get; set; } = 0.55f;
        public float StickReleaseThreshold { get; set; } = 0.25f;
        public float MouseMovementThreshold { get; set; } = 2f;
        public float MouseScrollThreshold { get; set; } = 0.1f;
        public double SwitchDebounceSeconds { get; set; } = 0.1;
        public bool TrackMouseMovement { get; set; } = true;
    }

    /// <summary>
    /// 실제 입력 이벤트를 관찰해 마지막 사용 장치를 판별합니다. 장치 연결 자체로 전환하지 않습니다.
    /// 이벤트를 소비하거나 가상 포인터 입력을 만들지 않으며, Dispose로 전역 구독을 해제합니다.
    /// 분할 화면에서는 deviceFilter로 해당 플레이어에 배정된 장치만 허용해야 합니다.
    /// </summary>
    public sealed class InputDeviceTracker : IDisposable
    {
        /// <summary>스틱이 충분히 중립으로 돌아와야 다음 입력으로 인정하기 위한 장치별 상태입니다.</summary>
        private sealed class StickState
        {
            public bool LeftEngaged;
            public bool RightEngaged;
        }

        private readonly Dictionary<int, StickState> _sticks = new Dictionary<int, StickState>();
        private readonly Func<InputDevice, bool> _deviceFilter;
        private readonly float _activationThreshold;
        private readonly float _releaseThreshold;
        private readonly float _mouseMovementThreshold;
        private readonly float _mouseScrollThreshold;
        private readonly double _debounceSeconds;
        private readonly bool _trackMouseMovement;
        private InputDevice _pendingDevice;
        private UiInputKind _pendingKind;
        private double _pendingSince;
        private bool _disposed;

        public event Action Changed;
        public InputDevice CurrentDevice { get; private set; }
        public UiInputKind CurrentKind { get; private set; } = UiInputKind.KeyboardAndMouse;

        public InputDeviceTracker(InputDeviceTrackerOptions options = null, Func<InputDevice, bool> deviceFilter = null)
        {
            options = options ?? new InputDeviceTrackerOptions();
            if (options.StickActivationThreshold <= 0 || options.StickActivationThreshold > 1 ||
                float.IsNaN(options.StickActivationThreshold) ||
                options.StickReleaseThreshold < 0 || options.StickReleaseThreshold >= options.StickActivationThreshold ||
                float.IsNaN(options.StickReleaseThreshold) ||
                options.MouseMovementThreshold < 0 || float.IsNaN(options.MouseMovementThreshold) ||
                float.IsInfinity(options.MouseMovementThreshold) ||
                options.MouseScrollThreshold < 0 || float.IsNaN(options.MouseScrollThreshold) ||
                float.IsInfinity(options.MouseScrollThreshold) ||
                options.SwitchDebounceSeconds < 0 || double.IsNaN(options.SwitchDebounceSeconds) ||
                double.IsInfinity(options.SwitchDebounceSeconds))
                throw new ArgumentOutOfRangeException(nameof(options), "Tracker thresholds must be finite and release must be below activation.");

            _activationThreshold = options.StickActivationThreshold;
            _releaseThreshold = options.StickReleaseThreshold;
            _mouseMovementThreshold = options.MouseMovementThreshold;
            _mouseScrollThreshold = options.MouseScrollThreshold;
            _debounceSeconds = options.SwitchDebounceSeconds;
            _trackMouseMovement = options.TrackMouseMovement;
            _deviceFilter = deviceFilter;
            InputApi.onEvent += OnInputEvent;
            InputApi.onAfterUpdate += OnAfterUpdate;
            InputApi.onDeviceChange += OnDeviceChange;
        }

        private void OnInputEvent(InputEventPtr inputEvent, InputDevice device)
        {
            if (_disposed || device == null || !device.enabled ||
                (!inputEvent.IsA<StateEvent>() && !inputEvent.IsA<DeltaStateEvent>()) ||
                InputState.currentUpdateType == InputUpdateType.Editor ||
                InputState.currentUpdateType == InputUpdateType.BeforeRender ||
                (_deviceFilter != null && !_deviceFilter(device))) return;

            if (!TryGetKind(device, out var kind)) return;
            var meaningful = false;

            if (device is Touchscreen touch)
            {
                // 위치·압력의 작은 변화가 아니라 각 손가락이 누르기 시작한 시점을 사용합니다.
                // 개별 TouchState 이벤트는 Input System이 primaryTouch 경로로만 읽도록 연결합니다.
                // 따라서 touches[i]만 읽으면 실제 모바일의 터치 시작을 놓칩니다.
                if (touch.primaryTouch.ReadValueFromEvent(inputEvent, out var touchState) &&
                    touchState.phase == UnityEngine.InputSystem.TouchPhase.Began && touchState.touchId != 0)
                {
                    var alreadyPressed = false;
                    for (var i = 0; i < touch.touches.Count; i++)
                    {
                        var current = touch.touches[i];
                        if (current.touchId.ReadValue() == touchState.touchId && current.press.isPressed)
                        {
                            alreadyPressed = true;
                            break;
                        }
                    }
                    meaningful = !alreadyPressed;
                }
                // 전체 TouchscreenState 또는 개별 컨트롤 delta를 사용하는 장치도 지원합니다.
                for (var i = 0; i < touch.touches.Count; i++)
                    meaningful |= WasPressed(touch.touches[i].press, inputEvent);
            }
            else
            {
                var controls = device.allControls;
                for (var i = 0; i < controls.Count; i++)
                {
                    if (controls[i] is ButtonControl button && !button.synthetic && !button.noisy)
                        meaningful |= WasPressed(button, inputEvent);
                }
            }

            if (device is Gamepad gamepad)
            {
                if (!_sticks.TryGetValue(device.deviceId, out var state))
                {
                    // Tracker 생성 전부터 기울어진 스틱을 새 입력으로 오인하지 않도록 이전 상태를 기준으로 시작합니다.
                    state = new StickState
                    {
                        LeftEngaged = gamepad.leftStick.ReadValue().magnitude >= _activationThreshold,
                        RightEngaged = gamepad.rightStick.ReadValue().magnitude >= _activationThreshold
                    };
                    _sticks.Add(device.deviceId, state);
                }
                meaningful |= CheckStick(gamepad.leftStick, inputEvent, ref state.LeftEngaged);
                meaningful |= CheckStick(gamepad.rightStick, inputEvent, ref state.RightEngaged);
            }
            else if (device is Mouse mouse)
            {
                if (_trackMouseMovement && mouse.delta.ReadValueFromEvent(inputEvent, out var delta))
                    meaningful |= delta.sqrMagnitude > _mouseMovementThreshold * _mouseMovementThreshold;
                if (mouse.scroll.ReadValueFromEvent(inputEvent, out var scroll))
                    meaningful |= scroll.sqrMagnitude > _mouseScrollThreshold * _mouseScrollThreshold;
            }

            if (meaningful) Observe(device, kind);
        }

        private static bool WasPressed(ButtonControl button, InputEventPtr inputEvent)
        {
            // onEvent는 상태 적용 전이므로 기존 상태와 이벤트 값을 비교해 새 눌림만 판별합니다.
            return !button.isPressed && button.ReadValueFromEvent(inputEvent, out var value) &&
                   value >= button.pressPointOrDefault;
        }

        private bool CheckStick(StickControl stick, InputEventPtr inputEvent, ref bool engaged)
        {
            if (!stick.ReadValueFromEvent(inputEvent, out var value)) return false;
            var magnitude = value.magnitude;
            if (magnitude <= _releaseThreshold)
            {
                engaged = false;
                return false;
            }
            if (engaged || magnitude < _activationThreshold) return false;
            engaged = true;
            return true;
        }

        private void Observe(InputDevice device, UiInputKind kind)
        {
            if (device == CurrentDevice)
            {
                _pendingDevice = null;
                return;
            }
            // 같은 분류의 keyboard↔mouse는 즉시 반영해 실제 사용 컨트롤 안내를 갱신합니다.
            if (CurrentDevice == null || kind == CurrentKind || _debounceSeconds == 0)
            {
                SetCurrent(device, kind);
                return;
            }
            if (_pendingDevice == device) return;
            _pendingDevice = device;
            _pendingKind = kind;
            _pendingSince = Time.realtimeSinceStartupAsDouble;
        }

        private void OnAfterUpdate()
        {
            if (_disposed || _pendingDevice == null || InputState.currentUpdateType == InputUpdateType.Editor) return;
            if (Time.realtimeSinceStartupAsDouble - _pendingSince < _debounceSeconds) return;
            if (_pendingDevice.added && _pendingDevice.enabled && (_deviceFilter == null || _deviceFilter(_pendingDevice)))
                SetCurrent(_pendingDevice, _pendingKind);
            else
                _pendingDevice = null;
        }

        private void SetCurrent(InputDevice device, UiInputKind kind)
        {
            _pendingDevice = null;
            CurrentDevice = device;
            CurrentKind = kind;
            Changed?.Invoke();
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (_disposed || (change != InputDeviceChange.Removed && change != InputDeviceChange.Disconnected &&
                              change != InputDeviceChange.Disabled)) return;
            _sticks.Remove(device.deviceId);
            if (_pendingDevice == device) _pendingDevice = null;
            if (CurrentDevice != device) return;
            // 제거된 장치를 더 이상 참조하지 않되, 단순 연결을 사용 증거로 삼아 다른 장치를 선택하지 않습니다.
            CurrentDevice = null;
            Changed?.Invoke();
        }

        internal static bool TryGetKind(InputDevice device, out UiInputKind kind)
        {
            kind = UiInputKind.KeyboardAndMouse;
            if (device is Keyboard || device is Mouse) return true;
            if (device is Gamepad) { kind = UiInputKind.Gamepad; return true; }
            if (device is Touchscreen) { kind = UiInputKind.Touch; return true; }
            return false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            InputApi.onEvent -= OnInputEvent;
            InputApi.onAfterUpdate -= OnAfterUpdate;
            InputApi.onDeviceChange -= OnDeviceChange;
            _pendingDevice = null;
            CurrentDevice = null;
            _sticks.Clear();
            Changed = null;
        }
    }
}
