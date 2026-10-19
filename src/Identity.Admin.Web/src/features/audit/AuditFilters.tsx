import { Button, Col, Form, Input, Row, Space } from 'antd';
import { isUserId } from '@/features/users/userId';
import { localDateInput, localInstant } from './localTime';
import {
  compareAuditInstants,
  isEventType,
  type AuditParameters,
} from './parameters';

type Values = Pick<
  AuditParameters,
  'actorUserId' | 'userId' | 'eventType' | 'from' | 'to'
>;
export function AuditFilters({
  values,
  onApply,
  onReset,
}: {
  values: AuditParameters;
  onApply: (values: Values) => void;
  onReset: () => void;
}) {
  const [form] = Form.useForm<Values>();
  const idRule = {
    validator: (_: unknown, value: string | undefined) =>
      !value?.trim() || isUserId(value.trim())
        ? Promise.resolve()
        : Promise.reject(new Error('Enter a nonempty UUID.')),
  };
  const dateRule = {
    validator: (_: unknown, value: string | undefined) =>
      !value || localInstant(value)
        ? Promise.resolve()
        : Promise.reject(new Error('Enter a valid local date and time.')),
  };
  const instant = (input: string, previous: string) =>
    input === localDateInput(previous)
      ? previous
      : input
        ? (localInstant(input) ?? '')
        : '';
  return (
    <Form<Values>
      form={form}
      layout="vertical"
      initialValues={{
        actorUserId: values.actorUserId,
        userId: values.userId,
        eventType: values.eventType,
        from: localDateInput(values.from),
        to: localDateInput(values.to),
      }}
      onFinish={(input) =>
        onApply({
          actorUserId: input.actorUserId?.trim().toLowerCase() ?? '',
          userId: input.userId?.trim().toLowerCase() ?? '',
          eventType: input.eventType?.trim() ?? '',
          from: instant(input.from ?? '', values.from),
          to: instant(input.to ?? '', values.to),
        })
      }
    >
      <Row gutter={16}>
        <Col xs={24} lg={12} xl={8}>
          <Form.Item name="actorUserId" label="Actor user ID" rules={[idRule]}>
            <Input maxLength={64} placeholder="Administrator UUID" allowClear />
          </Form.Item>
        </Col>
        <Col xs={24} lg={12} xl={8}>
          <Form.Item
            name="userId"
            label="Target / related user ID"
            rules={[idRule]}
            extra="Matches target or legacy related user."
          >
            <Input maxLength={64} placeholder="User UUID" allowClear />
          </Form.Item>
        </Col>
        <Col xs={24} xl={8}>
          <Form.Item
            name="eventType"
            label="Event type"
            rules={[
              {
                validator: (_, value: string | undefined) =>
                  !value?.trim() || isEventType(value.trim())
                    ? Promise.resolve()
                    : Promise.reject(
                        new Error(
                          'Use lowercase letters and underscores, up to 80 characters.',
                        ),
                      ),
              },
            ]}
          >
            <Input
              maxLength={80}
              placeholder="e.g. admin_disabled_user"
              allowClear
            />
          </Form.Item>
        </Col>
        <Col xs={24} lg={12}>
          <Form.Item
            name="from"
            label="From · local time (inclusive)"
            rules={[dateRule]}
          >
            <Input
              type="datetime-local"
              step="0.001"
              min="0001-01-01T00:00"
              max="9999-12-31T23:59:59.999"
            />
          </Form.Item>
        </Col>
        <Col xs={24} lg={12}>
          <Form.Item
            name="to"
            label="To · local time (exclusive)"
            dependencies={['from']}
            rules={[
              dateRule,
              {
                validator: (_, value: string | undefined) => {
                  const from = form.getFieldValue('from') as string | undefined;
                  return !value ||
                    !from ||
                    !localInstant(value) ||
                    !localInstant(from) ||
                    compareAuditInstants(
                      instant(value, values.to),
                      instant(from, values.from),
                    ) > 0
                    ? Promise.resolve()
                    : Promise.reject(new Error('To must be later than from.'));
                },
              },
            ]}
          >
            <Input
              type="datetime-local"
              step="0.001"
              min="0001-01-01T00:00"
              max="9999-12-31T23:59:59.999"
            />
          </Form.Item>
        </Col>
      </Row>
      <Form.Item>
        <Space wrap>
          <Button type="primary" htmlType="submit">
            Apply filters
          </Button>
          <Button onClick={onReset}>Reset filters</Button>
        </Space>
      </Form.Item>
    </Form>
  );
}
