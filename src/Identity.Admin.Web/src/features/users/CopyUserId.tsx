import { useState } from 'react';
import { CopyOutlined } from '@ant-design/icons';
import { Button, Typography } from 'antd';

export function CopyUserId({ value }: { value: string }) {
  const [status, setStatus] = useState<
    'idle' | 'copying' | 'copied' | 'failed'
  >('idle');
  async function copy() {
    setStatus('copying');
    try {
      await navigator.clipboard.writeText(value);
      setStatus('copied');
    } catch {
      setStatus('failed');
    }
  }
  return (
    <div className="user-id">
      <Typography.Text code>{value}</Typography.Text>
      <Button
        type="text"
        icon={<CopyOutlined aria-hidden="true" />}
        aria-label="Copy user ID"
        loading={status === 'copying'}
        disabled={status === 'copying'}
        onClick={() => {
          void copy();
        }}
      />
      <span role="status" aria-label="Copy feedback">
        {status === 'copied'
          ? 'User ID copied.'
          : status === 'failed'
            ? 'Copy unavailable. Select the ID to copy it.'
            : ''}
      </span>
    </div>
  );
}
