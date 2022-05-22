namespace ProcessLater {
    public readonly record struct FileEntity(string Path, EntityType Type) {
        public static readonly FileEntity None = new();

        public override string ToString() {
            return $"[{Type}]{Path}";
        }
    }
}
