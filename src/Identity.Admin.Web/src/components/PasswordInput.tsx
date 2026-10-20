import { EyeInvisibleOutlined, EyeOutlined } from '@ant-design/icons';
import { Input } from 'antd';
import type { ComponentProps } from 'react';

export function PasswordInput(props: ComponentProps<typeof Input.Password>) {
  return (
    <Input.Password
      {...props}
      size="large"
      iconRender={(visible) => (
        <span className="password-visibility-icon" aria-hidden="true">
          {visible ? <EyeOutlined /> : <EyeInvisibleOutlined />}
        </span>
      )}
    />
  );
}
