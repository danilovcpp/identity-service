import type { paths } from '@/api/schema';

type HttpMethod =
  'get' | 'post' | 'put' | 'patch' | 'delete' | 'head' | 'options';

// Browser console operations only. Native/BFF token endpoints are not SPA auth.
export type ApiPath =
  | Exclude<
      Extract<keyof paths, `/api/v1/admin/${string}`>,
      '/api/v1/admin/auth/csrf'
    >
  | '/api/v1/Auth/invitations/accept'
  | '/api/v1/Auth/password/reset';

export type ApiMethod<P extends ApiPath> = {
  [M in HttpMethod]: NonNullable<paths[P][M]> extends never ? never : M;
}[HttpMethod];

type Operation<P extends ApiPath, M extends ApiMethod<P>> = NonNullable<
  paths[P][M]
>;

type PathParameters<O> = O extends { parameters: { path: infer P } }
  ? P
  : never;
export type QueryParameters<P extends ApiPath, M extends ApiMethod<P>> =
  Operation<P, M> extends { parameters: { query?: infer Q } } ? Q : never;
type JsonBody<O> = O extends {
  requestBody: { content: { 'application/json': infer B } };
}
  ? B
  : never;

export type RequestOptions<P extends ApiPath, M extends ApiMethod<P>> = {
  signal?: AbortSignal;
} & ([PathParameters<Operation<P, M>>] extends [never]
  ? { path?: never }
  : { path: PathParameters<Operation<P, M>> }) &
  ([QueryParameters<P, M>] extends [never]
    ? { query?: never }
    : { query?: QueryParameters<P, M> }) &
  ([JsonBody<Operation<P, M>>] extends [never]
    ? { body?: never }
    : { body: JsonBody<Operation<P, M>> });

export type ApiResponse<P extends ApiPath, M extends ApiMethod<P>> =
  Operation<P, M> extends { responses: infer R }
    ? {
        [S in keyof R]: S extends 200 | 201 | 202 | 204
          ? R[S] extends { content: { 'application/json': infer B } }
            ? B
            : void
          : never;
      }[keyof R]
    : never;
