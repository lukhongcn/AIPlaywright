using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using ModuleWorkFlow.PlaywrightRunner.Actions.Login;

namespace ModuleWorkFlow.PlaywrightRunner
{
    internal static class LoginTestRunner
    {
        public static async Task<LoginResult> RunAsync(string action, string loginUrl, string userName, string password)
        {
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

                return await MesLoginHelper.EnsureLoginAsync(page, loginUrl, userName, password);
            }
            catch (Exception ex)
            {
                return new LoginResult
                {
                    Success = false,
                    Message = ex.Message,
                    CurrentUrl = page == null ? loginUrl : page.Url
                };
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
