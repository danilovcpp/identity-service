import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, beforeEach } from 'vitest';
import { matchMedia, setTestViewport } from '@/test/viewport';

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: matchMedia,
});
// jsdom cannot measure pseudo-elements; scrollbar measurement needs only the element.
const getComputedStyle = window.getComputedStyle.bind(window);
window.getComputedStyle = (element) => getComputedStyle(element);
beforeEach(() => setTestViewport(1280));

afterEach(cleanup);
