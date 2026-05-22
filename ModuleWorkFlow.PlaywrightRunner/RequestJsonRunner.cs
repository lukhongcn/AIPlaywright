using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using ModuleWorkFlow.AIHelp.Config;
using ModuleWorkFlow.PlaywrightRunner.Actions.Login;
using ModuleWorkFlow.PlaywrightRunner.Actions.Orders;
using ModuleWorkFlow.PlaywrightRunner.Actions.PartProcess;
using Newtonsoft.Json;

namespace ModuleWorkFlow.PlaywrightRunner
{
    internal static class RequestJsonRunner
    {
        public static async Task RunAsync(string[] args)
        {
            var action = "login_test";
            var loginUrl = Environment.GetEnvironmentVariable("MES_LOGIN_URL");
            var userName = Environment.GetEnvironmentVariable("MES_USER");
            var password = Environment.GetEnvironmentVariable("MES_PASSWORD");

            try
            {
                var request = LoadRequest(args);
                if (request != null)
                {
                    action = string.IsNullOrWhiteSpace(request.Action) ? string.Empty : request.Action.Trim();
                    if (request.Params != null)
                    {
                        if (!string.IsNullOrWhiteSpace(request.Params.LoginUrl))
                        {
                            loginUrl = request.Params.LoginUrl;
                        }

                        if (!string.IsNullOrWhiteSpace(request.Params.UserName))
                        {
                            userName = request.Params.UserName;
                        }

                        if (!string.IsNullOrWhiteSpace(request.Params.Password))
                        {
                            password = request.Params.Password;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(action))
                {
                    JsonConsoleWriter.WriteResult(false, string.Empty, "缺少 action", loginUrl);
                    return;
                }

                switch (action.ToLowerInvariant())
                {
                    case "login_test":
                        if (string.IsNullOrWhiteSpace(loginUrl))
                        {
                            loginUrl = "http://localhost:5008/Login.aspx";
                        }

                        if (string.IsNullOrWhiteSpace(userName))
                        {
                            JsonConsoleWriter.WriteResult(false, action, "請設定 userName 或環境變數 MES_USER", loginUrl);
                            return;
                        }

                        if (string.IsNullOrWhiteSpace(password))
                        {
                            JsonConsoleWriter.WriteResult(false, action, "請設定 password 或環境變數 MES_PASSWORD", loginUrl);
                            return;
                        }

                        var loginResult = await LoginTestRunner.RunAsync(action, loginUrl, userName, password);
                        JsonConsoleWriter.WriteResult(loginResult.Success, action, loginResult.Message, loginResult.CurrentUrl);
                        return;

                    case "order_design_new_dryrun":
                        var config = AppConfig.Load();
                        var loginRequest = CreateLoginRequest(request == null ? null : request.Params, config);
                        var orderRequest = CreateOrderDesignRequest(request == null ? null : request.Params, config);
                        var result = await RunOrderDesignNewDryRunAsync(loginRequest, orderRequest);
                        JsonConsoleWriter.WriteResult(result);
                        return;

                    case "part_process_move_dryrun":
                        config = AppConfig.Load();
                        loginRequest = CreateLoginRequest(request == null ? null : request.Params, config);
                        var moveRequest = CreatePartProcessMoveRequest(request == null ? null : request.Params, config);
                        var moveResult = await RunPartProcessMoveDryRunAsync(loginRequest, moveRequest);
                        Console.WriteLine(JsonConvert.SerializeObject(moveResult));
                        return;

                    default:
                        JsonConsoleWriter.WriteResult(false, action, "不支持的 action", loginUrl);
                        return;
                }
            }
            catch (Exception ex)
            {
                JsonConsoleWriter.WriteResult(false, action, ex.Message, loginUrl);
            }
        }

        private static RequestModel LoadRequest(string[] args)
        {
            if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                return null;
            }

            var requestPath = args[0].Trim();
            if (!File.Exists(requestPath))
            {
                throw new FileNotFoundException("找不到 request.json", requestPath);
            }

            var json = File.ReadAllText(requestPath);
            return JsonConvert.DeserializeObject<RequestModel>(json);
        }

        private static LoginRequestParams CreateLoginRequest(RequestParams requestParams, AppConfig config)
        {
            var baseUrl = requestParams == null ? null : requestParams.BaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl) &&
                config != null &&
                config.Mes != null &&
                !string.IsNullOrWhiteSpace(config.Mes.BaseUrl))
            {
                baseUrl = config.Mes.BaseUrl;
            }

            return new LoginRequestParams
            {
                LoginUrl = requestParams != null && !string.IsNullOrWhiteSpace(requestParams.LoginUrl)
                    ? requestParams.LoginUrl
                    : MesLoginHelper.BuildLoginUrl(baseUrl),
                UserName = requestParams == null ? null : requestParams.UserName,
                Password = requestParams == null ? null : requestParams.Password
            };
        }

        private static OrderDesignNewDryRunRequest CreateOrderDesignRequest(RequestParams requestParams, AppConfig config)
        {
            var baseUrl = requestParams == null ? null : requestParams.BaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl) &&
                config != null &&
                config.Mes != null &&
                !string.IsNullOrWhiteSpace(config.Mes.BaseUrl))
            {
                baseUrl = config.Mes.BaseUrl;
            }

            return new OrderDesignNewDryRunRequest
            {
                BaseUrl = baseUrl,
                Customer = requestParams == null ? null : requestParams.Customer,
                ItemNo = requestParams == null ? null : requestParams.ItemNo,
                Quantity = requestParams == null ? null : requestParams.Quantity,
                OrderDate = requestParams == null ? null : requestParams.OrderDate,
                DeliveryDate = requestParams == null ? null : requestParams.DeliveryDate,
                Remark = requestParams == null ? null : requestParams.Remark
            };
        }

        private static PartProcessMoveDryRunRequest CreatePartProcessMoveRequest(RequestParams requestParams, AppConfig config)
        {
            var baseUrl = requestParams == null ? null : requestParams.BaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl) &&
                config != null &&
                config.Mes != null &&
                !string.IsNullOrWhiteSpace(config.Mes.BaseUrl))
            {
                baseUrl = config.Mes.BaseUrl;
            }

            return new PartProcessMoveDryRunRequest
            {
                BaseUrl = baseUrl,
                ModuleId = requestParams == null ? null : requestParams.ModuleId,
                PartNo = requestParams == null ? null : requestParams.PartNo,
                PartNoList = requestParams == null ? null : requestParams.PartNoList,
                MenuId = requestParams == null ? null : requestParams.MenuId,
                PageIndex = requestParams == null ? null : requestParams.PageIndex,
                ListUrl = requestParams == null ? null : requestParams.ListUrl,
                FromPosition = requestParams == null ? null : requestParams.FromPosition,
                ToPosition = requestParams == null ? null : requestParams.ToPosition,
                Direction = requestParams == null ? null : requestParams.Direction,
                Steps = requestParams != null && requestParams.Steps.HasValue ? requestParams.Steps.Value : 1,
                ProcessName = requestParams == null ? null : requestParams.ProcessName
            };
        }

        private static async Task<OrderDesignActionResult> RunOrderDesignNewDryRunAsync(LoginRequestParams loginRequest, OrderDesignNewDryRunRequest orderRequest)
        {
            var failedResult = new OrderDesignActionResult
            {
                Action = "order_design_new_dryrun",
                Executed = false,
                Success = false,
                Status = "failed",
                Message = "缺少登入資訊。"
            };

            if (loginRequest == null || string.IsNullOrWhiteSpace(loginRequest.LoginUrl))
            {
                failedResult.Message = "缺少登入網址，無法執行訂單新增 dry-run。";
                return failedResult;
            }

            if (string.IsNullOrWhiteSpace(loginRequest.UserName) || string.IsNullOrWhiteSpace(loginRequest.Password))
            {
                failedResult.Message = "缺少登入資訊，請提供 userName/password。";
                return failedResult;
            }

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

                var loginResult = await MesLoginHelper.EnsureLoginAsync(page, loginRequest.LoginUrl, loginRequest.UserName, loginRequest.Password);
                if (!loginResult.Success)
                {
                    failedResult.CurrentUrl = loginResult.CurrentUrl;
                    failedResult.Message = loginResult.Message;
                    return failedResult;
                }

                return await OrderDesignNewDryRunRunner.RunAsync(page, orderRequest);
            }
            catch (Exception ex)
            {
                failedResult.Status = "error";
                failedResult.Message = ex.Message;
                failedResult.CurrentUrl = page == null ? string.Empty : page.Url;
                return failedResult;
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

        private static async Task<PartProcessMoveDryRunResult> RunPartProcessMoveDryRunAsync(LoginRequestParams loginRequest, PartProcessMoveDryRunRequest moveRequest)
        {
            var failedResult = new PartProcessMoveDryRunResult
            {
                Action = "part_process_move_dryrun",
                Executed = false,
                Success = false,
                Status = "failed",
                Message = "缺少登入資訊。",
                Saved = false
            };

            if (loginRequest == null || string.IsNullOrWhiteSpace(loginRequest.LoginUrl))
            {
                failedResult.Message = "缺少登入網址，無法執行零件工藝移動 dry-run。";
                return failedResult;
            }

            if (string.IsNullOrWhiteSpace(loginRequest.UserName) || string.IsNullOrWhiteSpace(loginRequest.Password))
            {
                failedResult.Message = "缺少登入資訊，請提供 userName/password。";
                return failedResult;
            }

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

                var loginResult = await MesLoginHelper.EnsureLoginAsync(page, loginRequest.LoginUrl, loginRequest.UserName, loginRequest.Password);
                if (!loginResult.Success)
                {
                    failedResult.CurrentUrl = loginResult.CurrentUrl;
                    failedResult.Message = loginResult.Message;
                    return failedResult;
                }

                return await PartProcessMoveDryRunRunner.RunAsync(page, moveRequest);
            }
            catch (Exception ex)
            {
                failedResult.Status = "error";
                failedResult.Message = ex.Message;
                failedResult.CurrentUrl = page == null ? string.Empty : page.Url;
                return failedResult;
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
    }
}
