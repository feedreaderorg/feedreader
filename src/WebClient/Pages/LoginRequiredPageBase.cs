using FeedReader.WebClient.Models;
using Microsoft.AspNetCore.Components;

namespace FeedReader.WebClient.Pages
{
    public class LoginRequiredPageBase : ComponentBase
    {
        [Inject]
        public NavigationManager NavigationManager { get; set; }

        [CascadingParameter]
        public User CurrentUser { get; set; }

        protected override void OnInitialized()
        {
            if (CurrentUser.Role == UserRole.Guest)
            {
                NavigationManager.NavigateTo("/login");
            }
        }

        protected override void OnParametersSet()
        {
            if (CurrentUser.Role == UserRole.Guest)
            {
                NavigationManager.NavigateTo("/login");
            }
        }
    }
}
