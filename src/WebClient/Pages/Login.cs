
using System;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Components;

namespace FeedReader.WebClient.Pages
{
    public partial class LogIn : Microsoft.AspNetCore.Components.ComponentBase
    {
        [Parameter]
        public string OAuthProvider { get; set; }
    
        [Inject]
        public NavigationManager NavigationManager { get; set; }

        protected override async Task OnInitializedAsync()
        {
            if (!string.IsNullOrEmpty(OAuthProvider))
            {
                try
                {
                    await App.CurrentUser.LoginAsync(ExtractJwtTokenFromCallbackUri(NavigationManager.Uri));
                    NavigationManager.NavigateTo("/");
                }
                catch (Exception ex)
                {
                    var title = HttpUtility.UrlEncode("Login Failed");
                    var detail = HttpUtility.UrlEncode(ex.Message);
                    var backTitle = HttpUtility.UrlEncode("Back to Login");
                    var backUri = HttpUtility.UrlEncode("/login");
                    NavigationManager.NavigateTo($"/error?title={title}&detail={detail}&backTitle={backTitle}&backUri={backUri}");
                }
            }
        }

        private string ExtractJwtTokenFromCallbackUri(string callbackUri)
        {
            // Get jwt token from the uri.
            var fragment = callbackUri.Substring(callbackUri.IndexOf('#') + 1);
            var queries = HttpUtility.ParseQueryString(fragment);
            var token = queries["id_token"];
            if (token == null)
            {
                var error = queries["error"];
                if (error == null)
                {
                    throw new Exception($"Unexpected error in callback uri: {callbackUri}.");
                }
                else
                {
                    throw new Exception($"Get error from callback uri: {error}");
                }
            }
            else
            {
                return token;
            }
        }
    }
}