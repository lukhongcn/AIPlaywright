using ModuleWorkFlow.AIHelp.AI;
using ModuleWorkFlow.AIHelp.Training;

namespace ModuleWorkFlow.PlaywrightRunner
{
    public  static class TrainingExecutionResultFactory
    {
        public static TrainingExecutionResult Create(
            string status,
            bool success,
            string message,
            string currentUrl,
            AiDecision decision)
        {
            var result = new TrainingExecutionResult
            {
                Executed = false,
                Status = status,
                Success = success,
                Message = message,
                CurrentUrl = currentUrl
            };

            if (decision != null && decision.Params != null)
            {
                string userName;
                string password;

                if (decision.Params.TryGetValue("userName", out userName))
                {
                    result.UserName = userName;
                }

                if (decision.Params.TryGetValue("password", out password))
                {
                    result.Password = password;
                }
            }

            return result;
        }

        public static TrainingExecutionResult FromLoginResult(
            LoginResult loginResult,
            string userName,
            string password)
        {
            return new TrainingExecutionResult
            {
                Executed = true,
                Status = loginResult != null && loginResult.Success ? "executed" : "failed",
                Success = loginResult != null && loginResult.Success,
                Message = loginResult == null ? "沒有執行結果" : loginResult.Message,
                CurrentUrl = loginResult == null ? null : loginResult.CurrentUrl,
                UserName = userName,
                Password = password
            };
        }
    }
}
