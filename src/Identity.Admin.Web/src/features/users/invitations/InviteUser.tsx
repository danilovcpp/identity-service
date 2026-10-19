import { useId, useState } from 'react';
import { UserAddOutlined } from '@ant-design/icons';
import { App, Button, Form, Input, Modal, Typography } from 'antd';
import { ApiError } from '@/api/errors';
import { containTabFocus } from '@/components/containTabFocus';
import { useAuth } from '@/features/auth/authContext';
import { InvitationError } from './InvitationError';
import { useInvitationCommand } from './useInvitationCommand';
import { hasControlCharacters } from '../validation';

type Values = { email: string; displayName?: string };

function InviteDialog({
  open,
  onClose,
}: {
  open: boolean;
  onClose: () => void;
}) {
  const [form] = Form.useForm<Values>();
  const formId = useId();
  const command = useInvitationCommand();
  const { message } = App.useApp();
  async function submit(values: Values) {
    try {
      if (
        await command.execute({
          kind: 'invite',
          input: {
            email: values.email.trim(),
            displayName: values.displayName?.trim() || null,
          },
        })
      ) {
        onClose();
        void message.success('Invitation accepted for processing.');
      }
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        const fields = Object.keys(error.fieldErrors).map((name) =>
          name.toLowerCase(),
        );
        form.setFields([
          ...(fields.includes('email')
            ? [{ name: 'email' as const, errors: ['Check the email address.'] }]
            : []),
          ...(fields.includes('displayname')
            ? [
                {
                  name: 'displayName' as const,
                  errors: [
                    'Use at most 120 characters without control characters.',
                  ],
                },
              ]
            : []),
        ]);
      }
    }
  }
  return (
    <Modal
      open={open}
      destroyOnHidden
      afterClose={command.reset}
      title="Invite user"
      okText="Send invitation"
      cancelText="Cancel"
      confirmLoading={command.pending}
      okButtonProps={{
        form: formId,
        htmlType: 'submit',
        disabled: command.pending,
        'aria-label': 'Send invitation',
      }}
      cancelButtonProps={{ disabled: command.pending }}
      onCancel={command.pending ? undefined : onClose}
      closable={!command.pending}
      keyboard={!command.pending}
      mask={{ closable: false }}
      focusable={{ trap: true, focusTriggerAfterClose: true }}
      wrapProps={{ onKeyDown: containTabFocus }}
    >
      <Typography.Paragraph>
        Identity will request an invitation email. The recipient sets their own
        password using a secure link. Submitting confirms this invitation
        request; email delivery is not guaranteed.
      </Typography.Paragraph>
      <InvitationError error={command.error} />
      <Form
        form={form}
        id={formId}
        clearOnDestroy
        layout="vertical"
        requiredMark={false}
        disabled={command.pending}
        onFinish={(values) => void submit(values)}
      >
        <Form.Item
          name="email"
          label="Email"
          rules={[
            {
              required: true,
              whitespace: true,
              message: 'Enter an email address.',
            },
            {
              transform: (value: string) => value.trim(),
              type: 'email',
              message: 'Enter a valid email address.',
            },
            { max: 320, message: 'Use at most 320 characters.' },
            {
              validator: (_, value: string | undefined) =>
                value && hasControlCharacters(value)
                  ? Promise.reject(new Error('Remove control characters.'))
                  : Promise.resolve(),
            },
          ]}
        >
          <Input
            type="email"
            autoFocus
            autoComplete="off"
            autoCapitalize="none"
            spellCheck={false}
            maxLength={320}
          />
        </Form.Item>
        <Form.Item
          name="displayName"
          label="Display name (optional)"
          rules={[
            { max: 120, message: 'Use at most 120 characters.' },
            {
              validator: (_, value: string | undefined) =>
                value && hasControlCharacters(value)
                  ? Promise.reject(new Error('Remove control characters.'))
                  : Promise.resolve(),
            },
          ]}
        >
          <Input maxLength={120} autoComplete="off" />
        </Form.Item>
      </Form>
    </Modal>
  );
}

export function InviteUser() {
  const { canWrite } = useAuth();
  const [open, setOpen] = useState(false);
  if (!canWrite) return null;
  return (
    <>
      <Button
        type="primary"
        icon={<UserAddOutlined aria-hidden="true" />}
        onClick={() => setOpen(true)}
      >
        Invite user
      </Button>
      <InviteDialog open={open} onClose={() => setOpen(false)} />
    </>
  );
}
