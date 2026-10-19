import { useRef, useState } from 'react';
import { App, Button, Form, Input, Typography } from 'antd';
import { ApiError } from '@/api/errors';
import { ConfirmAction } from '@/components/ConfirmAction';
import { useAuth } from '@/features/auth/authContext';
import { hasControlCharacters } from '../validation';
import { StatusCommandError } from './StatusCommandError';
import { useAccountStatusCommand } from './useAccountStatusCommand';

type Action = 'disable' | 'enable';
type Values = { reason: string };
type Props = { userId: string; email: string; status: string };

function StatusAction({
  userId,
  email,
  action,
}: Omit<Props, 'status'> & { action: Action }) {
  const [open, setOpen] = useState(false);
  const confirmedDialog = useRef(false);
  const [form] = Form.useForm<Values>();
  const command = useAccountStatusCommand();
  const { message } = App.useApp();
  const disabling = action === 'disable';
  async function submit(values?: Values) {
    // Form validation is async. Cancel must also cancel a not-yet-started submit.
    if (!confirmedDialog.current) return;
    try {
      const input = disabling
        ? {
            action: 'disable' as const,
            userId,
            reason: values?.reason.trim() ?? '',
          }
        : { action: 'enable' as const, userId };
      if (await command.execute(input)) {
        setOpen(false);
        confirmedDialog.current = false;
        void message.success(
          disabling
            ? 'Account disabled.'
            : 'Account enabled. Identity will refresh its status.',
        );
      }
    } catch (error) {
      if (
        disabling &&
        error instanceof ApiError &&
        error.status === 400 &&
        Object.keys(error.fieldErrors).some(
          (field) => field.toLowerCase() === 'reason',
        )
      ) {
        form.setFields([
          {
            name: 'reason',
            errors: [
              'Enter a reason of at most 500 characters without control characters.',
            ],
          },
        ]);
      }
    }
  }
  return (
    <>
      <Button
        danger={disabling}
        onClick={() => {
          command.reset();
          confirmedDialog.current = true;
          setOpen(true);
        }}
      >
        {disabling ? 'Disable account' : 'Enable account'}
      </Button>
      <ConfirmAction
        open={open}
        title={disabling ? 'Disable account?' : 'Enable account?'}
        confirmText={disabling ? 'Confirm disable' : 'Confirm enable'}
        danger={disabling}
        description={
          <>
            <Typography.Text strong>{email}</Typography.Text>
            <br />
            {disabling
              ? 'This blocks new sign-ins and refreshes, revokes all refresh and administrator sessions, and invalidates outstanding one-time links. External access tokens can remain valid until they expire.'
              : 'Identity determines the restored status. Pending verification and invited accounts remain unverified. Revoked sessions and invalidated links are not restored.'}
          </>
        }
        pending={command.pending}
        onConfirm={() => {
          if (disabling) form.submit();
          else void submit();
        }}
        onCancel={() => {
          confirmedDialog.current = false;
          setOpen(false);
        }}
      >
        <div className="account-status-confirm">
          <StatusCommandError error={command.error} />
          {disabling && (
            <Form
              form={form}
              clearOnDestroy
              layout="vertical"
              requiredMark={false}
              disabled={command.pending}
              onFinish={(values) => void submit(values)}
            >
              <Form.Item
                name="reason"
                label="Reason"
                extra="Up to 500 characters, on one line."
                rules={[
                  {
                    required: true,
                    whitespace: true,
                    message: 'Enter a reason for disabling this account.',
                  },
                  { max: 500, message: 'Use at most 500 characters.' },
                  {
                    validator: (_, value: string | undefined) =>
                      value && hasControlCharacters(value)
                        ? Promise.reject(
                            new Error(
                              'Remove line breaks and control characters.',
                            ),
                          )
                        : Promise.resolve(),
                  },
                ]}
              >
                <Input.TextArea
                  maxLength={500}
                  autoSize={{ minRows: 2, maxRows: 5 }}
                />
              </Form.Item>
            </Form>
          )}
        </div>
      </ConfirmAction>
    </>
  );
}

export function AccountStatusAction({ userId, email, status }: Props) {
  const { canWrite } = useAuth();
  const action =
    status === 'Disabled'
      ? 'enable'
      : ['Active', 'PendingVerification', 'Invited'].includes(status)
        ? 'disable'
        : null;
  return canWrite && action ? (
    <StatusAction
      key={`${userId}:${action}`}
      userId={userId}
      email={email}
      action={action}
    />
  ) : null;
}
