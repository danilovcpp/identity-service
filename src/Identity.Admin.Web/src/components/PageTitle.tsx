import { useEffect, useRef } from 'react';
import { Typography } from 'antd';

// Route headings provide a keyboard/screen-reader destination without moving
// focus on filter changes, refetches or other updates within the same page.
export function PageTitle({ children }: { children: string }) {
  const heading = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    document.title = `${children} · Identity Console`;
    heading.current?.focus({ preventScroll: true });
  }, [children]);
  return (
    <Typography.Title level={1} ref={heading} tabIndex={-1}>
      {children}
    </Typography.Title>
  );
}
