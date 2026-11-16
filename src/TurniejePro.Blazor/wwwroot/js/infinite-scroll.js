// Doczytywanie listy przy przewijaniu: obserwujemy znacznik na końcu listy
// i wywołujemy OnReachEnd w komponencie Blazor, gdy wjedzie w widok.
window.tpInfiniteScroll = (() => {
    let observer = null;

    return {
        observe(element, dotNetRef) {
            this.disconnect();
            if (!element) {
                return;
            }

            observer = new IntersectionObserver(entries => {
                if (entries.some(entry => entry.isIntersecting)) {
                    dotNetRef.invokeMethodAsync('OnReachEnd');
                }
            }, { rootMargin: '200px' });

            observer.observe(element);
        },

        disconnect() {
            if (observer) {
                observer.disconnect();
                observer = null;
            }
        }
    };
})();
