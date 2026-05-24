using ModuleWorkFlow.AIHelp.AI;
using ModuleWorkFlow.AIHelp.Config;
using ModuleWorkFlow.AIHelp.Llm;
using ModuleWorkFlow.AIHelp.Tools;
using ModuleWorkFlow.AIHelp.Training;
using ModuleWorkFlow.PlaywrightRunner.Actions.Login;
using ModuleWorkFlow.PlaywrightRunner.Actions.Orders;
using ModuleWorkFlow.PlaywrightRunner.Infrastructure;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ModuleWorkFlow.PlaywrightRunner
{
    internal static class AiConsoleRunner
    {
        public static async Task RunAsync()
        {
            var config = AppConfig.Load();
            var toolRegistry = ToolRegistry.Load();
            var trainingDataLogger = new TrainingDataLogger();
            var cleanTrainingRetriever = new CleanTrainingRetriever();
            var maskUserName = false;

            Console.WriteLine("請告訴我你要做什麼，我會幫你操作 MES。");
            Console.WriteLine("例如：新增訂單，客戶 A01，料號 P001，數量 10");
            Console.Write("> ");
            var userInput = UnicodeConsoleHelper.ReadLine();

            if (string.IsNullOrWhiteSpace(userInput))
            {
                Console.WriteLine("輸入不能為空");
                return;
            }

            var llmClient = new OpenAiCompatibleLlmClient(config.Llm);
            var systemPrompt = BuildPromptWithRetrievedExamples(
                AiPromptBuilder.BuildSystemPrompt(toolRegistry),
                cleanTrainingRetriever,
                userInput);

            Console.WriteLine("正在調用千問解析指令...");

            var aiRawText = await llmClient.ChatAsync(systemPrompt, userInput);
            var decision = AiDecisionParser.Parse(aiRawText, config, toolRegistry, "AI 原始返回：");

            if (IsRejectedOrError(decision))
            {
                trainingDataLogger.Append(
                    systemPrompt,
                    toolRegistry,
                    userInput,
                    aiRawText,
                    decision,
                    TrainingExecutionResultFactory.Create(decision.Status, false, decision.Question, null, decision),
                    maskUserName);

                if (decision != null &&
                    string.Equals(decision.Status, "rejected", StringComparison.OrdinalIgnoreCase))
                {
                    var correctedDecision = TryResolveRejectedDecisionFromSelection(toolRegistry, userInput);
                    if (correctedDecision != null)
                    {
                        decision = correctedDecision;
                        aiRawText = SerializeDecisionForPromptReuse(correctedDecision);
                        trainingDataLogger.AppendCorrectedCleanSample(
                            systemPrompt,
                            userInput,
                            correctedDecision,
                            TrainingExecutionResultFactory.Create(correctedDecision.Status, false, "使用者手動選擇 action 進行糾正。", null, correctedDecision),
                            maskUserName);
                    }
                    else
                    {
                        Console.WriteLine(decision.Question);
                        return;
                    }
                }
                else
                {
                    Console.WriteLine(decision.Question);
                    return;
                }
            }

            if (!IsReady(decision))
            {
                trainingDataLogger.Append(
                    systemPrompt,
                    toolRegistry,
                    userInput,
                    aiRawText,
                    decision,
                    TrainingExecutionResultFactory.Create("awaiting_more_info", false, "等待補充資訊", null, decision),
                    maskUserName);
            }

            var retryCount = 0;
            while (!IsReady(decision))
            {
                retryCount++;

                if (retryCount > 3)
                {
                    trainingDataLogger.Append(
                        systemPrompt,
                        toolRegistry,
                        userInput,
                        aiRawText,
                        decision,
                        TrainingExecutionResultFactory.Create("cancelled", false, "補充資訊次數過多，已取消。", null, decision),
                        maskUserName);
                    Console.WriteLine("我還是無法確認你的需求，這次先取消。請換個說法再試一次。");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(decision.Question))
                {
                    Console.WriteLine(decision.Question);
                }
                else
                {
                    Console.WriteLine("我還需要一些資訊才能繼續，請補充一下。");
                }

                if (decision.MissingParams != null && decision.MissingParams.Count > 0)
                {
                Console.WriteLine("目前還缺這些資料：" + string.Join(", ", decision.MissingParams));
            }

            Console.Write("請直接補充說明 > ");
            var moreInput = UnicodeConsoleHelper.ReadLine();

                if (string.IsNullOrWhiteSpace(moreInput))
                {
                    trainingDataLogger.Append(
                        systemPrompt,
                        toolRegistry,
                        userInput,
                        aiRawText,
                        decision,
                        TrainingExecutionResultFactory.Create("cancelled", false, "沒有輸入補充資訊，已取消。", null, decision),
                        maskUserName);
                    Console.WriteLine("你這次沒有補充內容，所以先取消。");
                    return;
                }

                userInput = userInput + "\n補充資訊：" + moreInput;
                systemPrompt = BuildPromptWithRetrievedExamples(
                    AiPromptBuilder.BuildSystemPrompt(toolRegistry),
                    cleanTrainingRetriever,
                    userInput);

                Console.WriteLine("正在重新調用千問解析補充資訊...");
                aiRawText = await llmClient.ChatAsync(systemPrompt, userInput);
                decision = AiDecisionParser.Parse(aiRawText, config, toolRegistry, "AI 返回：");

                if (IsRejectedOrError(decision))
                {
                    trainingDataLogger.Append(
                        systemPrompt,
                        toolRegistry,
                        userInput,
                        aiRawText,
                        decision,
                        TrainingExecutionResultFactory.Create(decision.Status, false, decision.Question, null, decision),
                        maskUserName);

                    if (decision != null &&
                        string.Equals(decision.Status, "rejected", StringComparison.OrdinalIgnoreCase))
                    {
                        var correctedDecision = TryResolveRejectedDecisionFromSelection(toolRegistry, userInput);
                        if (correctedDecision != null)
                        {
                            decision = correctedDecision;
                            aiRawText = SerializeDecisionForPromptReuse(correctedDecision);
                            trainingDataLogger.AppendCorrectedCleanSample(
                                systemPrompt,
                                userInput,
                                correctedDecision,
                                TrainingExecutionResultFactory.Create(correctedDecision.Status, false, "使用者手動選擇 action 進行糾正。", null, correctedDecision),
                                maskUserName);
                            continue;
                        }
                    }

                    Console.WriteLine(decision.Question);
                    return;
                }

                if (!IsReady(decision))
                {
                    trainingDataLogger.Append(
                        systemPrompt,
                        toolRegistry,
                        userInput,
                        aiRawText,
                        decision,
                        TrainingExecutionResultFactory.Create("awaiting_more_info", false, "等待補充資訊", null, decision),
                        maskUserName);
                }
            }

            if (string.Equals(decision.Action, "login_test", StringComparison.OrdinalIgnoreCase))
            {
                await ExecuteLoginTestAsync(
                    decision,
                    trainingDataLogger,
                    systemPrompt,
                    toolRegistry,
                    userInput,
                    aiRawText,
                    maskUserName);
                return;
            }

            if (string.Equals(decision.Action, "order_design_new_dryrun", StringComparison.OrdinalIgnoreCase))
            {
                await ExecuteOrderDesignDryRunAsync(
                    config,
                    decision,
                    trainingDataLogger,
                    systemPrompt,
                    toolRegistry,
                    userInput,
                    aiRawText,
                    maskUserName);
                return;
            }

            trainingDataLogger.Append(
                systemPrompt,
                toolRegistry,
                userInput,
                aiRawText,
                decision,
                TrainingExecutionResultFactory.Create("rejected", false, "不支持的 action：" + decision.Action, null, decision),
                maskUserName);
            Console.WriteLine("不支持的 action：" + decision.Action);
        }

        private static async Task ExecuteLoginTestAsync(
            AiDecision decision,
            TrainingDataLogger trainingDataLogger,
            string systemPrompt,
            ToolRegistry toolRegistry,
            string userInput,
            string aiRawText,
            bool maskUserName)
        {
            string loginUrl;
            string userName;
            string password;

            if (!TryGetLoginParams(decision, trainingDataLogger, systemPrompt, toolRegistry, userInput, aiRawText, maskUserName,
                out loginUrl, out userName, out password))
            {
                return;
            }

            var loginResult = await LoginTestRunner.RunAsync(decision.Action, loginUrl, userName, password);
            trainingDataLogger.Append(
                systemPrompt,
                toolRegistry,
                userInput,
                aiRawText,
                decision,
                TrainingExecutionResultFactory.FromLoginResult(loginResult, userName, password),
                maskUserName);

            JsonConsoleWriter.WriteResult(loginResult.Success, decision.Action, loginResult.Message, loginResult.CurrentUrl);
        }

        private static async Task ExecuteOrderDesignDryRunAsync(
            AppConfig config,
            AiDecision decision,
            TrainingDataLogger trainingDataLogger,
            string systemPrompt,
            ToolRegistry toolRegistry,
            string userInput,
            string aiRawText,
            bool maskUserName)
        {
            var actionDefinition = toolRegistry.GetAction(decision.Action);
            var baseUrl = config != null && config.Mes != null ? config.Mes.BaseUrl : null;
            var userName = Environment.GetEnvironmentVariable("MES_USER");
            var password = Environment.GetEnvironmentVariable("MES_PASSWORD");

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                LogInvalid(trainingDataLogger, systemPrompt, toolRegistry, userInput, aiRawText, decision, "缺少 Mes.BaseUrl", maskUserName);
                return;
            }

            if (actionDefinition != null && actionDefinition.RequiresLogin)
            {
                if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
                {
                    Console.WriteLine("需要先登入 MES。請輸入登入資訊，格式：帳號 密碼，例如：admin 123456");
                    Console.Write("> ");
                    var loginInput = UnicodeConsoleHelper.ReadLine();
                    FillMesCredentialsFromSingleLine(loginInput, ref userName, ref password);
                }

                EnsureMesCredentialsFromConsole(ref userName, ref password);

                if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
                {
                    trainingDataLogger.Append(
                        systemPrompt,
                        toolRegistry,
                        userInput,
                        aiRawText,
                        decision,
                        TrainingExecutionResultFactory.Create("failed", false, "需要先登入 MES，但沒有提供完整登入資訊。", null, decision),
                        maskUserName);
                    Console.WriteLine("需要先登入 MES，但你沒有提供完整登入資訊。");
                    return;
                }
            }

            var request = CreateOrderDesignRequest(baseUrl, decision);
            IPlaywright playwright = null;
            IBrowser browser = null;
            IPage page = null;

            try
            {
                playwright = await Playwright.CreateAsync();
                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Channel = "msedge",
                    Headless = false,
                    SlowMo = 300
                });
                page = await browser.NewPageAsync();

                if (actionDefinition != null && actionDefinition.RequiresLogin)
                {
                    var loginResult = await MesLoginHelper.EnsureLoginAsync(page, baseUrl, userName, password);
                    if (!loginResult.Success)
                    {
                        trainingDataLogger.Append(
                            systemPrompt,
                            toolRegistry,
                            userInput,
                            aiRawText,
                            decision,
                            TrainingExecutionResultFactory.Create("failed", false, "登入失敗，未執行新增訂單。", loginResult.CurrentUrl, decision),
                            maskUserName);
                        Console.WriteLine("登入失敗，沒有繼續新增訂單。請檢查帳號或密碼後再試一次。");
                        return;
                    }
                }

                var result = await OrderDesignNewDryRunRunner.RunAsync(page, request);
                trainingDataLogger.Append(
                    systemPrompt,
                    toolRegistry,
                    userInput,
                    aiRawText,
                    decision,
                    TrainingExecutionResultFactory.Create(result.Status, result.Success, result.Message, result.CurrentUrl, decision),
                    maskUserName);

                Console.WriteLine(MapOrderDesignOutputMessage(result));
            }
            catch (Exception ex)
            {
                trainingDataLogger.Append(
                    systemPrompt,
                    toolRegistry,
                    userInput,
                    aiRawText,
                    decision,
                    TrainingExecutionResultFactory.Create("error", false, ex.Message, page == null ? null : page.Url, decision),
                    maskUserName);
                Console.WriteLine(ex.Message);
            }
            finally
            {
                if (browser != null)
                {
                    await browser.CloseAsync();
                }

                if (playwright != null)
                {
                    playwright.Dispose();
                }
            }
        }

        private static OrderDesignNewDryRunRequest CreateOrderDesignRequest(string baseUrl, AiDecision decision)
        {
            var request = new OrderDesignNewDryRunRequest
            {
                BaseUrl = baseUrl
            };

            if (decision == null || decision.Params == null)
            {
                return request;
            }

            string value;

            if (decision.Params.TryGetValue("customer", out value))
            {
                request.Customer = value;
            }

            if (decision.Params.TryGetValue("itemNo", out value))
            {
                request.ItemNo = value;
            }

            if (decision.Params.TryGetValue("quantity", out value))
            {
                request.Quantity = value;
            }

            if (decision.Params.TryGetValue("orderDate", out value))
            {
                request.OrderDate = value;
            }

            if (decision.Params.TryGetValue("deliveryDate", out value))
            {
                request.DeliveryDate = value;
            }

            if (decision.Params.TryGetValue("remark", out value))
            {
                request.Remark = value;
            }

            return request;
        }

        private static string MapOrderDesignOutputMessage(OrderDesignActionResult result)
        {
            if (result == null)
            {
                return "新增訂單失敗。";
            }

            if (string.Equals(result.Status, "prepared", StringComparison.OrdinalIgnoreCase))
            {
                return "訂單資料已填好，但我沒有點保存。請你在 MES 頁面檢查資料，確認無誤後手動點保存。";
            }

            if (string.Equals(result.Status, "partial_prepared", StringComparison.OrdinalIgnoreCase))
            {
                return "我已經進入新增頁，但有些欄位沒有自動填上。請你在 MES 頁面檢查後再決定是否保存。";
            }

            return result.Message;
        }

        private static void EnsureMesCredentialsFromConsole(ref string userName, ref string password)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                Console.Write("請輸入 MES 帳號 > ");
                userName = UnicodeConsoleHelper.ReadLine();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                Console.Write("請輸入 MES 密碼 > ");
                password = UnicodeConsoleHelper.ReadLine();
            }
        }

        private static void FillMesCredentialsFromSingleLine(string input, ref string userName, ref string password)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return;
            }

            var parts = input.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && string.IsNullOrWhiteSpace(userName))
            {
                userName = parts[0];
            }

            if (parts.Length > 1 && string.IsNullOrWhiteSpace(password))
            {
                password = parts[1];
            }
        }

        private static bool TryGetLoginParams(
            AiDecision decision,
            TrainingDataLogger trainingDataLogger,
            string systemPrompt,
            ToolRegistry toolRegistry,
            string userInput,
            string aiRawText,
            bool maskUserName,
            out string loginUrl,
            out string userName,
            out string password)
        {
            loginUrl = null;
            userName = null;
            password = null;

            if (decision.Params == null)
            {
                LogInvalid(trainingDataLogger, systemPrompt, toolRegistry, userInput, aiRawText, decision, "缺少 params", maskUserName);
                return false;
            }

            if (!decision.Params.TryGetValue("loginUrl", out loginUrl) ||
                string.IsNullOrWhiteSpace(loginUrl))
            {
                LogInvalid(trainingDataLogger, systemPrompt, toolRegistry, userInput, aiRawText, decision, "缺少 loginUrl", maskUserName);
                return false;
            }

            if (!decision.Params.TryGetValue("userName", out userName) ||
                string.IsNullOrWhiteSpace(userName))
            {
                LogInvalid(trainingDataLogger, systemPrompt, toolRegistry, userInput, aiRawText, decision, "缺少 userName", maskUserName);
                return false;
            }

            if (!decision.Params.TryGetValue("password", out password) ||
                string.IsNullOrWhiteSpace(password))
            {
                LogInvalid(trainingDataLogger, systemPrompt, toolRegistry, userInput, aiRawText, decision, "缺少 password", maskUserName);
                return false;
            }

            return true;
        }

        private static void LogInvalid(
            TrainingDataLogger trainingDataLogger,
            string systemPrompt,
            ToolRegistry toolRegistry,
            string userInput,
            string aiRawText,
            AiDecision decision,
            string message,
            bool maskUserName)
        {
            trainingDataLogger.Append(
                systemPrompt,
                toolRegistry,
                userInput,
                aiRawText,
                decision,
                TrainingExecutionResultFactory.Create("invalid_decision", false, message, null, decision),
                maskUserName);

            Console.WriteLine(message);
        }

        private static string BuildPromptWithRetrievedExamples(
            string baseSystemPrompt,
            CleanTrainingRetriever cleanTrainingRetriever,
            string userInput)
        {
            if (cleanTrainingRetriever == null || string.IsNullOrWhiteSpace(userInput))
            {
                return baseSystemPrompt ?? string.Empty;
            }

            var matches = cleanTrainingRetriever.FindMatches(userInput, 3);
            if (matches == null || matches.Count == 0)
            {
                return baseSystemPrompt ?? string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(baseSystemPrompt ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("以下是與目前輸入相近的歷史樣本，僅供你參考判斷，不可盲目照抄：");

            var index = 1;
            foreach (var match in matches)
            {
                builder.AppendLine("樣本 " + index + "：");
                builder.AppendLine("User: " + (match.UserInput ?? string.Empty));
                builder.AppendLine("Assistant: " + (match.AssistantOutput ?? string.Empty));

                if (!string.IsNullOrWhiteSpace(match.Action) ||
                    !string.IsNullOrWhiteSpace(match.Status) ||
                    !string.IsNullOrWhiteSpace(match.SampleType))
                {
                    var metadataParts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(match.Action))
                    {
                        metadataParts.Add("action=" + match.Action);
                    }

                    if (!string.IsNullOrWhiteSpace(match.Status))
                    {
                        metadataParts.Add("status=" + match.Status);
                    }

                    if (!string.IsNullOrWhiteSpace(match.SampleType))
                    {
                        metadataParts.Add("sampleType=" + match.SampleType);
                    }

                    builder.AppendLine("Metadata: " + string.Join(", ", metadataParts));
                }

                builder.AppendLine();
                index++;
            }

            return builder.ToString();
        }

        private static AiDecision TryResolveRejectedDecisionFromSelection(
            ToolRegistry toolRegistry,
            string userInput)
        {
            var actions = toolRegistry == null ? null : toolRegistry.GetAllActions();
            if (actions == null || actions.Count == 0)
            {
                return null;
            }

            Console.WriteLine("目前沒有自動識別成功。請從以下 action 中選一個，或直接按 Enter 取消：");
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                Console.WriteLine(string.Format("{0}. {1} - {2}", i + 1, action.Action, action.Description));
            }

            Console.Write("請輸入編號 > ");
            var selection = UnicodeConsoleHelper.ReadLine();
            if (string.IsNullOrWhiteSpace(selection))
            {
                return null;
            }

            int index;
            if (!int.TryParse(selection.Trim(), out index))
            {
                Console.WriteLine("輸入不是有效編號，這次先取消。");
                return null;
            }

            if (index < 1 || index > actions.Count)
            {
                Console.WriteLine("超出可選範圍，這次先取消。");
                return null;
            }

            var selectedAction = actions[index - 1];
            var correctedDecision = CreateManualDecisionFromAction(selectedAction);

            Console.WriteLine("已將「" + userInput + "」修正為 action：" + correctedDecision.Action);
            return correctedDecision;
        }

        private static AiDecision CreateManualDecisionFromAction(ActionToolDefinition actionDefinition)
        {
            var decision = new AiDecision
            {
                Status = "ready",
                Action = actionDefinition == null ? string.Empty : actionDefinition.Action,
                Question = string.Empty,
                Confidence = 1,
                Params = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                MissingParams = new List<string>()
            };

            if (actionDefinition != null && actionDefinition.RequiredParams != null)
            {
                foreach (var requiredParam in actionDefinition.RequiredParams)
                {
                    decision.MissingParams.Add(requiredParam);
                }
            }

            if (decision.MissingParams.Count > 0)
            {
                decision.Status = "need_more_info";
                decision.Question = "请补充以下参数: " + string.Join(", ", decision.MissingParams);
            }

            return decision;
        }

        private static string SerializeDecisionForPromptReuse(AiDecision decision)
        {
            if (decision == null)
            {
                return string.Empty;
            }

            return Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                status = decision.Status,
                action = decision.Action,
                @params = decision.Params,
                missingParams = decision.MissingParams,
                question = decision.Question,
                confidence = decision.Confidence
            });
        }

        private static bool IsReady(AiDecision decision)
        {
            return decision != null &&
                   string.Equals(decision.Status, "ready", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRejectedOrError(AiDecision decision)
        {
            return decision != null &&
                   (string.Equals(decision.Status, "rejected", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(decision.Status, "error", StringComparison.OrdinalIgnoreCase));
        }
    }
}
