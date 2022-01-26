export function initialize(lastItemIndicator, component) {
    var observer = new IntersectionObserver(async entries => {
        for (var entry of entries) {
            if (entry.isIntersecting) {
                await component.invokeMethodAsync("LoadMoreItems");
                break;
            }
        }
    });

    observer.observe(lastItemIndicator);

    return {
        dispose: () => {
            observer.disconnect();
        },
        onNewItems: () => {
            observer.unobserve(lastIndicator);
            observer.observe(lastIndicator);
        },
    };
}