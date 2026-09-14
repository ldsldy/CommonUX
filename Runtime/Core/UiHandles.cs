using System;
using System.Runtime.CompilerServices;

namespace CommonUX.Core
{
    /// <summary>한 UiContext 안에서 등록된 화면을 식별합니다. 저장용 ID가 아니며 외부 enum/asset과 독립적입니다.</summary>
    public readonly struct ScreenHandle : IEquatable<ScreenHandle>
    {
        internal UiContext Owner { get; }
        internal int Id { get; }

        internal ScreenHandle(UiContext owner, int id) { Owner = owner; Id = id; }

        /// <summary>기본값이 아닌 발급된 handle인지 확인합니다. 등록 해제 여부는 context가 검증합니다.</summary>
        public bool IsValid => Owner != null && Id != 0;
        public bool Equals(ScreenHandle other) => ReferenceEquals(Owner, other.Owner) && Id == other.Id;
        public override bool Equals(object obj) => obj is ScreenHandle other && Equals(other);
        public override int GetHashCode() => unchecked(((Owner == null ? 0 : RuntimeHelpers.GetHashCode(Owner)) * 397) ^ Id);
        public override string ToString() => IsValid ? "Screen#" + Id : "Screen(None)";
        public static bool operator ==(ScreenHandle left, ScreenHandle right) => left.Equals(right);
        public static bool operator !=(ScreenHandle left, ScreenHandle right) => !left.Equals(right);
    }

    /// <summary>한 UiContext가 소유한 stack을 식별하는 실행 중 handle입니다.</summary>
    public readonly struct StackHandle : IEquatable<StackHandle>
    {
        internal UiContext Owner { get; }
        internal int Id { get; }

        internal StackHandle(UiContext owner, int id) { Owner = owner; Id = id; }
        public bool IsValid => Owner != null && Id != 0;
        public bool Equals(StackHandle other) => ReferenceEquals(Owner, other.Owner) && Id == other.Id;
        public override bool Equals(object obj) => obj is StackHandle other && Equals(other);
        public override int GetHashCode() => unchecked(((Owner == null ? 0 : RuntimeHelpers.GetHashCode(Owner)) * 397) ^ Id);
        public override string ToString() => IsValid ? "Stack#" + Id : "Stack(None)";
        public static bool operator ==(StackHandle left, StackHandle right) => left.Equals(right);
        public static bool operator !=(StackHandle left, StackHandle right) => !left.Equals(right);
    }
}
