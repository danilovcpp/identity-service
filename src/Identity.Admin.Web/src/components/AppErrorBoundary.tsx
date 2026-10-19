import { Component, type PropsWithChildren } from 'react';
import { ErrorState } from '@/components/ErrorState';

export class AppErrorBoundary extends Component<
  PropsWithChildren,
  { failed: boolean }
> {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  render() {
    return this.state.failed ? (
      <main className="public-content">
        <ErrorState onRetry={() => this.setState({ failed: false })} />
      </main>
    ) : (
      this.props.children
    );
  }
}
