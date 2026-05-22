using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace ModuleWorkFlow.PlaywrightRunner.Actions.Login
{
    internal static class MesLoginHelper
    {
        public static async Task<LoginResult> EnsureLoginAsync(IPage page, string baseUrl, string userName, string password)
        {
            if (page == null)
            {
                throw new ArgumentNullException("page");
            }

            var loginUrl = BuildLoginUrl(baseUrl);
            await page.GotoAsync(loginUrl);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await page.Locator("#TextBox_UserName").FillAsync(userName ?? string.Empty);
            await page.Locator("#HTML_Password").FillAsync(password ?? string.Empty);
            await page.Locator("#Button_Login").ClickAsync();

            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
                {
                    Timeout = 5000
                });
            }
            catch
            {
            }

            if (page.Url.IndexOf("Login.aspx", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return new LoginResult
                {
                    Success = true,
                    Message = "登入成功",
                    CurrentUrl = page.Url
                };
            }

            var messageText = "登入失敗";
            var message = page.Locator("#Label_Message");
            if (await message.CountAsync() > 0)
            {
                var text = (await message.InnerTextAsync()).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    messageText = text;
                }
            }

            return new LoginResult
            {
                Success = false,
                Message = messageText,
                CurrentUrl = page.Url
            };
        }

        public static string BuildLoginUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return null;
            }

            var normalized = baseUrl.Trim();
            if (normalized.EndsWith("/Login.aspx", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return normalized.TrimEnd('/') + "/Login.aspx";
        }
    }
}
