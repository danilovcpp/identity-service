import { Alert, Button, Card, Form, Input, Typography } from 'antd';
import { useRef, useState } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { ApiError, ClientError, isRecord } from '@/api/errors';
import { PageHeader } from '@/components/PageHeader';
import { useAuth } from './authContext';
import { SessionState } from './AuthGate';
import { safeReturnPath } from './returnPath';

type Values = { email: string; password: string };

export function LoginPage() {
  const auth = useAuth();
  const location = useLocation();
  const [form] = Form.useForm<Values>();
  const [error, setError] = useState<unknown>(null);
  const submitting = useRef(false);
  const returnTo = safeReturnPath(
    isRecord(location.state) ? location.state.returnTo : undefined,
  );
  if (auth.status === 'authenticated')
    return <Navigate to={returnTo} replace />;
  if (auth.status !== 'unauthenticated') return <SessionState />;

  async function submit(values: Values) {
    if (submitting.current) return;
    submitting.current = true;
    setError(null);
    try {
      await auth.signIn({
        email: values.email.trim(),
        password: values.password,
      });
    } catch (error) {
      setError(error);
    } finally {
      // Passwords never enter a Query mutation, URL, storage or notifications.
      form.resetFields(['password']);
      submitting.current = false;
    }
  }

  return (
    <div className="login-page">
      <PageHeader
        title="Sign in to Identity"
        description="Use your administrator account to continue."
      />
      <Card>
        {error !== null && (
          <Alert
            className="login-error"
            type="error"
            showIcon
            title="Unable to sign in"
            description={
              error instanceof ApiError && error.status === 401
                ? 'Unable to sign in with these credentials.'
                : error instanceof ApiError && error.code === 'csrf_invalid'
                  ? 'Your security check expired. Please submit the form again.'
                  : error instanceof ApiError || error instanceof ClientError
                    ? error.message
                    : 'Please try again.'
            }
          />
        )}
        <Form
          form={form}
          layout="vertical"
          requiredMark={false}
          onFinish={(values) => void submit(values)}
          disabled={auth.pending}
        >
          <Form.Item
            name="email"
            label="Email"
            rules={[
              { required: true, message: 'Enter your email address.' },
              { type: 'email', message: 'Enter a valid email address.' },
              { max: 320, message: 'Email must be 320 characters or fewer.' },
            ]}
          >
            <Input
              type="email"
              autoComplete="username"
              maxLength={320}
              autoCapitalize="none"
              spellCheck={false}
            />
          </Form.Item>
          <Form.Item
            name="password"
            label="Password"
            rules={[{ required: true, message: 'Enter your password.' }]}
          >
            <Input.Password autoComplete="current-password" />
          </Form.Item>
          <Button
            aria-label="Sign in"
            type="primary"
            htmlType="submit"
            block
            loading={auth.pending}
            disabled={auth.pending}
          >
            Sign in
          </Button>
        </Form>
      </Card>
      <Typography.Paragraph className="login-caption" type="secondary">
        Access is limited to authorized Identity administrators.
      </Typography.Paragraph>
    </div>
  );
}
