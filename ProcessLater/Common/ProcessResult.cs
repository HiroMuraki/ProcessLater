namespace ProcessLater {
    public class ProcessResult {
        public FileEntity Entity { get; set; } = FileEntity.None;
        public State State { get; set; } = State.None;
        public string ErrorMessage { get; set; } = string.Empty;

        public ProcessResult() { }
    };
}
