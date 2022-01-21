// Show loading proress.
// See https://docs.microsoft.com/en-us/aspnet/core/blazor/fundamentals/startup?view=aspnetcore-6.0
export function beforeStart(options, extensions) {
    options.loadBootResource = (() => {
        var downloadingResources = 0;
        var totalResources = 0;
        return (type, name, defaultUri, integrity) => {
            if (type == "dotnetjs") {
                return defaultUri;
            }

            ++totalResources;
            var resource = window.fetch(defaultUri, {
                integrity: integrity
            });
            resource.then(response => {
                ++downloadingResources;
                document.getElementById("progressbar").style.width = parseInt(downloadingResources * 100 / totalResources) + "%";
            });
            return resource;
        };
    })();
}