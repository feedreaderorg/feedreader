export function initialize(lastItemIndicator, component) {
    var observer = new IntersectionObserver(async entries => {
        for (var entry of entries) {
            if (entry.isIntersecting) {
                await component.invokeMethodAsync("LoadMoreItems");
                break;
            }
        }
    }, {
        root: findClosestScrollContainer(lastItemIndicator),
        rootMargin: '0px',
        threshold: 0
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

function findClosestScrollContainer(element) {
    while (element) {
        const style = getComputedStyle(element);
        if (style.overflowY !== 'visible') {
            return element;
        }
        element = element.parentElement;
    }
    return null;
}