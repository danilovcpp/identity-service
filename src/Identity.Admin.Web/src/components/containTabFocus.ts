import type { KeyboardEvent } from 'react';

// Ant Design's focusin lock cannot catch Tab moving to browser chrome. Keep the
// boundary Tab inside the overlay; Ant Design still owns initial focus.
export function containTabFocus(event: KeyboardEvent<HTMLDivElement>) {
  if (event.key !== 'Tab') return;
  const elements = Array.from(
    event.currentTarget.querySelectorAll<HTMLElement>(
      'a[href],button,[tabindex]',
    ),
  ).filter(
    (element) =>
      element.tabIndex >= 0 &&
      !element.matches(':disabled') &&
      element.getClientRects().length > 0 &&
      !element.closest('[aria-hidden="true"],[inert]'),
  );
  const first = elements[0];
  const last = elements.at(-1);
  if (
    first &&
    last &&
    ((event.shiftKey && event.target === first) ||
      (!event.shiftKey && event.target === last))
  ) {
    event.preventDefault();
    (event.shiftKey ? last : first).focus();
  }
}
