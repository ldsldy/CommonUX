using System;
using System.Collections.Generic;
using CommonUX.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using InputApi = UnityEngine.InputSystem.InputSystem;

namespace CommonUX.InputSystem
{
    /// <summary>텍스트 안내와 선택적 사용자 제공 이미지를 담은 해석 결과입니다.</summary>
    public readonly struct ActionHint
    {
        public string Text { get; }
        public Texture2D Texture { get; }
        public string DeviceLayout { get; }
        public string ControlPath { get; }
        public bool IsBound { get; }

        public ActionHint(string text, Texture2D texture, string deviceLayout, string controlPath, bool isBound)
        {
            Text = text ?? string.Empty;
            Texture = texture;
            DeviceLayout = deviceLayout;
            ControlPath = controlPath;
            IsBound = isBound;
        }
    }

    /// <summary>
    /// 명령의 입력 안내를 현재 장치와 최신 리바인딩 정보로 해석합니다. 액션을 실행하지 않습니다.
    /// Changed를 ActionBar.Refresh에 연결하고 GetHint를 SetHintProvider에 전달할 수 있습니다.
    /// </summary>
    public sealed class ActionHintResolver : IDisposable
    {
        /// <summary>입력 액션과 선택적인 control scheme binding group 필터를 연결합니다.</summary>
        private sealed class Registration
        {
            public InputAction Action;
            public string BindingGroup;
        }

        private readonly Dictionary<UiCommand, Registration> _registrations = new Dictionary<UiCommand, Registration>();
        private readonly InputDeviceTracker _tracker;
        private readonly GlyphSet _glyphs;
        private bool _disposed;
        private bool _resolving;

        public event Action Changed;

        public ActionHintResolver(InputDeviceTracker tracker, GlyphSet glyphs = null)
        {
            _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
            _glyphs = glyphs;
            _tracker.Changed += OnChanged;
            InputApi.onActionChange += OnActionChange;
        }

        /// <summary>안내만 등록하므로 Submit/Cancel도 지정할 수 있습니다. 입력 실행 연결과는 독립적입니다.</summary>
        public void Bind(UiCommand command, InputAction action, string bindingGroup = null)
        {
            ThrowIfDisposed();
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (action == null) throw new ArgumentNullException(nameof(action));
            _registrations[command] = new Registration { Action = action, BindingGroup = bindingGroup };
            Changed?.Invoke();
        }

        public void Unbind(UiCommand command)
        {
            ThrowIfDisposed();
            if (command != null && _registrations.Remove(command)) Changed?.Invoke();
        }

        public string GetHint(UiCommand command) => Resolve(command).Text;

        public ActionHint Resolve(UiCommand command)
        {
            ThrowIfDisposed();
            if (command == null || !_registrations.TryGetValue(command, out var registration))
                return new ActionHint(string.Empty, null, null, null, false);
            // controls의 지연 바인딩 해석 중 onActionChange가 재진입하는 경우를 방지합니다.
            _resolving = true;
            try
            {
                var index = FindBinding(registration, out var matchedDevice);
                if (index < 0) return new ActionHint(string.Empty, null, null, null, false);
                var text = registration.Action.GetBindingDisplayString(index, out var layout, out var path);
                if (matchedDevice != null && !string.IsNullOrEmpty(layout)) layout = matchedDevice.layout;
                Texture2D texture = null;
                if (_glyphs != null && _glyphs.TryResolve(layout, path, out texture, out var fallback) && !string.IsNullOrEmpty(fallback))
                    text = fallback;
                // 아이콘이 없어도 Input System의 표시명을 반환하므로 외부 이미지가 필수는 아닙니다.
                return new ActionHint(text, texture, layout, path, true);
            }
            finally { _resolving = false; }
        }

        private int FindBinding(Registration registration, out InputDevice matchedDevice)
        {
            matchedDevice = null;
            var action = registration.Action;
            var bindings = action.bindings;
            var device = _tracker.CurrentDevice;
            if (device != null)
            {
                var controls = action.controls;
                // keyboard와 mouse는 한 입력 분류입니다. 마우스 클릭 뒤에도 키보드 단축키를 표시합니다.
                // 정확히 일치하는 장치를 먼저 찾고, 없으면 같은 keyboard/mouse 분류를 찾습니다.
                for (var pass = 0; pass < 2; pass++)
                {
                    if (pass == 1 && _tracker.CurrentKind != UiInputKind.KeyboardAndMouse) break;
                    for (var i = 0; i < controls.Count; i++)
                    {
                        var candidate = controls[i].device;
                        if (pass == 0 && candidate != device) continue;
                        if (pass == 1 && !(candidate is Keyboard) && !(candidate is Mouse)) continue;
                        var index = action.GetBindingIndexForControl(controls[i]);
                        if (index < 0 || !MatchesGroup(bindings[index].groups, registration.BindingGroup)) continue;
                        // composite의 부분 키가 아니라 "Shift+B"처럼 전체 의미를 표시합니다.
                        while (index > 0 && bindings[index].isPartOfComposite) index--;
                        matchedDevice = candidate;
                        return index;
                    }
                }
                return -1;
            }

            // 첫 입력 전에는 기본 분류로 표시하고, 연결되어 있다는 이유만으로 장치를 선택하지 않습니다.
            for (var i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].isComposite || string.IsNullOrEmpty(bindings[i].effectivePath) ||
                    !MatchesGroup(bindings[i].groups, registration.BindingGroup) ||
                    !MatchesActionMasks(action, bindings[i])) continue;
                var layout = InputControlPath.TryGetDeviceLayout(bindings[i].effectivePath);
                if (!MatchesKind(layout, _tracker.CurrentKind)) continue;
                while (i > 0 && bindings[i].isPartOfComposite) i--;
                return i;
            }
            return -1;
        }

        private static bool MatchesActionMasks(InputAction action, InputBinding binding)
        {
            // 아직 실제 장치가 정해지지 않았더라도 호출자가 제한한 control scheme을 존중합니다.
            if (action.bindingMask.HasValue && !action.bindingMask.Value.Matches(binding)) return false;
            var map = action.actionMap;
            if (map == null) return true;
            if (map.bindingMask.HasValue && !map.bindingMask.Value.Matches(binding)) return false;
            return map.asset == null || !map.asset.bindingMask.HasValue || map.asset.bindingMask.Value.Matches(binding);
        }

        private static bool MatchesKind(string layout, UiInputKind kind)
        {
            if (string.IsNullOrEmpty(layout)) return false;
            switch (kind)
            {
                case UiInputKind.Gamepad: return InputApi.IsFirstLayoutBasedOnSecond(layout, "Gamepad");
                case UiInputKind.Touch: return InputApi.IsFirstLayoutBasedOnSecond(layout, "Touchscreen");
                default: return InputApi.IsFirstLayoutBasedOnSecond(layout, "Keyboard") || InputApi.IsFirstLayoutBasedOnSecond(layout, "Mouse");
            }
        }

        private static bool MatchesGroup(string groups, string required)
        {
            if (string.IsNullOrEmpty(required)) return true;
            if (string.IsNullOrEmpty(groups)) return false;
            var start = 0;
            while (start <= groups.Length)
            {
                var end = groups.IndexOf(';', start);
                if (end < 0) end = groups.Length;
                if (end - start == required.Length && string.Compare(groups, start, required, 0, required.Length, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;
                if (end == groups.Length) break;
                start = end + 1;
            }
            return false;
        }

        private void OnActionChange(object changed, InputActionChange change)
        {
            if (_disposed || _resolving || change != InputActionChange.BoundControlsChanged) return;
            foreach (var registration in _registrations.Values)
            {
                var action = registration.Action;
                if (ReferenceEquals(changed, action) || ReferenceEquals(changed, action.actionMap) ||
                    (action.actionMap != null && ReferenceEquals(changed, action.actionMap.asset)))
                {
                    Changed?.Invoke();
                    return;
                }
            }
        }

        private void OnChanged() { if (!_disposed) Changed?.Invoke(); }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(ActionHintResolver)); }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _tracker.Changed -= OnChanged;
            InputApi.onActionChange -= OnActionChange;
            _registrations.Clear();
            Changed = null;
        }
    }
}
