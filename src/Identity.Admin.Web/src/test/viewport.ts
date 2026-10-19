let viewportWidth = 1280;
export function setTestViewport(width: number) {
  viewportWidth = width;
}

// jsdom has no layout engine. Browser smoke verifies actual responsive rendering.
export function matchMedia(query: string): MediaQueryList {
  const min = /min-width:\s*([\d.]+)px/.exec(query)?.[1];
  const max = /max-width:\s*([\d.]+)px/.exec(query)?.[1];
  return {
    media: query,
    matches:
      Boolean(min || max) &&
      (!min || viewportWidth >= Number(min)) &&
      (!max || viewportWidth <= Number(max)),
    onchange: null,
    addListener: () => undefined,
    removeListener: () => undefined,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    dispatchEvent: () => true,
  };
}
