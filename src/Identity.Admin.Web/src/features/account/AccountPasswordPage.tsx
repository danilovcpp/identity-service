import { Alert, Button, Card, Form, Result, Typography } from 'antd';
import { PasswordInput } from '@/components/PasswordInput';
import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '@/api/errors';
import { PageHeader } from '@/components/PageHeader';
import { PageTitle } from '@/components/PageTitle';
import type { AccountFlow, AccountLink, LinkState } from './accountLink';
import { setAccountPassword } from './api';
import { passwordHint, passwordProblem } from './passwordPolicy';

type Values = { newPassword: string; confirmation: string };

export function AccountPasswordPage({
  flow,
  initialLink,
}: {
  flow: AccountFlow;
  initialLink?: AccountLink;
}) {
  const [link, setLink] = useState<LinkState>(
    () => initialLink?.read('/account/' + flow) ?? { problem: 'missing' },
  );
  const [form] = Form.useForm<Values>();
  const [pending, setPending] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState<string>();
  const flight = useRef<AbortController | null>(null);
  useLayoutEffect(() => {
    initialLink?.release();
    return initialLink?.subscribe((entry) => {
      const next = entry.read('/account/' + flow);
      flight.current?.abort();
      flight.current = null;
      form.resetFields();
      setLink(next);
      setSuccess(false);
      setError(undefined);
      setPending(false);
    });
  }, [initialLink, flow, form]);
  useEffect(() => () => flight.current?.abort(), []);
  const activation = flow === 'activate';
  const action = activation ? 'Activate account' : 'Reset password';

  async function submit(values: Values) {
    if (flight.current || !link.token) return;
    const controller = new AbortController();
    flight.current = controller;
    setPending(true);
    setError(undefined);
    try {
      await setAccountPassword(
        flow,
        link.token,
        values.newPassword,
        AbortSignal.any([controller.signal, AbortSignal.timeout(30_000)]),
      );
      if (controller.signal.aborted) return;
      setLink({ problem: 'missing' });
      setSuccess(true);
    } catch (failure) {
      if (controller.signal.aborted) return;
      if (
        failure instanceof ApiError &&
        ['invalid_invitation_token', 'invalid_password_reset_token'].includes(
          failure.code,
        )
      ) {
        setLink({ problem: 'invalid' });
      } else if (
        failure instanceof ApiError &&
        failure.code === 'validation_failed'
      ) {
        setError(
          'Identity did not accept this password. Follow the password policy and choose a less common password.',
        );
      } else if (failure instanceof ApiError && failure.status === 429) {
        setError('Too many attempts. Wait before submitting again.');
      } else {
        setError(
          'The result could not be confirmed. Check your connection and try again. If the link was already used, request a new one.',
        );
      }
    } finally {
      if (!controller.signal.aborted) {
        form.resetFields();
        setPending(false);
      }
      if (flight.current === controller) flight.current = null;
    }
  }

  if (success)
    return (
      <section role="status" aria-label="Account update completed">
        <Result
          status="success"
          title={
            <PageTitle>
              {activation ? 'Account activated' : 'Password changed'}
            </PageTitle>
          }
          subTitle="Sign in through the application that uses your Identity account. Identity administrators can use the console sign-in below."
          extra={<Link to="/admin/login">Administrator sign in</Link>}
        />
      </section>
    );
  if (link.problem)
    return (
      <section role="alert" aria-label="Account link required">
        <Result
          status="warning"
          title={
            <PageTitle>
              {link.problem === 'missing'
                ? 'Link required'
                : 'Link unavailable'}
            </PageTitle>
          }
          subTitle={
            link.problem === 'missing'
              ? 'Open the complete link from your Identity email to continue.'
              : 'This link is invalid, expired or has already been used. Ask your Identity administrator for a new link.'
          }
        />
      </section>
    );
  return (
    <div className="login-page">
      <PageHeader
        title={activation ? 'Activate your account' : 'Choose a new password'}
        description={
          activation
            ? 'Set your password to accept the Identity invitation.'
            : 'Set a new password for your Identity account. Existing sessions will be revoked.'
        }
      />
      <Card>
        {error && (
          <Alert
            className="login-error"
            type="error"
            showIcon
            title="Unable to complete the request"
            description={error}
          />
        )}
        <Typography.Paragraph id="password-policy" type="secondary">
          {passwordHint}
        </Typography.Paragraph>
        <Form<Values>
          form={form}
          layout="vertical"
          requiredMark={false}
          disabled={pending}
          onFinish={(values) => void submit(values)}
        >
          <Form.Item
            name="newPassword"
            label="New password"
            rules={[
              {
                validator: (_, value: string | undefined) => {
                  const problem = passwordProblem(value ?? '');
                  return problem
                    ? Promise.reject(new Error(problem))
                    : Promise.resolve();
                },
              },
            ]}
          >
            <PasswordInput
              autoComplete="new-password"
              aria-describedby="password-policy"
            />
          </Form.Item>
          <Form.Item
            name="confirmation"
            label="Confirm password"
            dependencies={['newPassword']}
            rules={[
              { required: true, message: 'Confirm your new password.' },
              ({ getFieldValue }) => ({
                validator: (_, value: string | undefined) =>
                  !value || getFieldValue('newPassword') === value
                    ? Promise.resolve()
                    : Promise.reject(new Error('The passwords do not match.')),
              }),
            ]}
          >
            <PasswordInput autoComplete="new-password" />
          </Form.Item>
          <Button
            aria-label={action}
            type="primary"
            htmlType="submit"
            block
            loading={pending}
            disabled={pending}
          >
            {action}
          </Button>
        </Form>
      </Card>
    </div>
  );
}
