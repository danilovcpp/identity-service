import { QueryClientProvider } from '@tanstack/react-query';
import { App as AntApp, ConfigProvider } from 'antd';
import enUS from 'antd/locale/en_US';
import { useState, type PropsWithChildren } from 'react';
import { createQueryClient } from '@/app/queryClient';
import { consoleTheme } from '@/app/theme';

export function AppProviders({ children }: PropsWithChildren) {
  const [queryClient] = useState(createQueryClient);
  return (
    <QueryClientProvider client={queryClient}>
      <ConfigProvider
        theme={consoleTheme}
        locale={enUS}
        card={{ styles: { title: { whiteSpace: 'normal' } } }}
      >
        <AntApp className="identity-app">{children}</AntApp>
      </ConfigProvider>
    </QueryClientProvider>
  );
}
