import { Modal, Typography } from 'antd';
import type { ReactNode } from 'react';
import { containTabFocus } from '@/components/containTabFocus';

type ConfirmActionProps = {
  open: boolean;
  title: string;
  description: ReactNode;
  confirmText: string;
  danger?: boolean;
  pending?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
  afterClose?: () => void;
  children?: ReactNode;
};

// Controlled presentation only: the feature owns permission, mutation and errors.
export function ConfirmAction({
  open,
  title,
  description,
  confirmText,
  danger = false,
  pending = false,
  onConfirm,
  onCancel,
  afterClose,
  children,
}: ConfirmActionProps) {
  return (
    <Modal
      open={open}
      title={title}
      onOk={pending ? undefined : onConfirm}
      onCancel={pending ? undefined : onCancel}
      afterClose={afterClose}
      okText={confirmText}
      cancelText="Cancel"
      confirmLoading={pending}
      okButtonProps={{ danger, disabled: pending, 'aria-label': confirmText }}
      cancelButtonProps={{ disabled: pending, autoFocus: true }}
      closable={!pending}
      keyboard={!pending}
      mask={{ closable: false }}
      focusable={{ trap: true, focusTriggerAfterClose: true }}
      wrapProps={{ onKeyDown: containTabFocus }}
      destroyOnHidden
    >
      <Typography.Paragraph>{description}</Typography.Paragraph>
      {children}
    </Modal>
  );
}
