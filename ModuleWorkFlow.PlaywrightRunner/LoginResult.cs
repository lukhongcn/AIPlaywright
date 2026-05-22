namespace ModuleWorkFlow.PlaywrightRunner
{
    public sealed class LoginResult
    {
        public bool Success { get; set; }

        public string Message { get; set; }

        public string CurrentUrl { get; set; }
    }
}
