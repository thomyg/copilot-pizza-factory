import { AadHttpClient, type AadHttpClientFactory, type HttpClientResponse, type IHttpClientOptions } from '@microsoft/sp-http';

/**
 * Where the running Copilot Pizza Factory lives.
 *
 * Nonna never opens the store backend herself — she reads TrattoriaSoft ERP 3000
 * through the factory's service hatch (/api/nonna/*). One constant, so re-pointing
 * this package at another deployment is a one-line change plus a rebuild.
 */
export const FACTORY_API_BASE = 'https://trattoria-copilotpizzafactory.azurewebsites.net';

/**
 * The Entra application the factory sits behind (App Service built-in auth).
 * SPFx trades the signed-in user's SharePoint identity for a token with this
 * audience; the factory is not reachable without one.
 */
export const FACTORY_RESOURCE = 'api://7ba457d6-e4fe-41ed-adfc-17d98f57af4b';

/** How long a surface waits for the factory before falling back to rehearsal data. */
export const FACTORY_TIMEOUT_MS = 8000;

/**
 * The factory's front door, with the user's token attached.
 *
 * Everything here goes through AadHttpClient, so the call carries a bearer token
 * for FACTORY_RESOURCE and App Service auth lets it through. A demo must never
 * hang on a cold backend, so every call is raced against a hard timeout and the
 * callers degrade to rehearsal data rather than spin.
 */
export class FactoryHttp {
  private _client: Promise<AadHttpClient> | undefined;

  public constructor(
    private readonly _factory: AadHttpClientFactory,
    private readonly _resource: string = FACTORY_RESOURCE,
    private readonly _timeoutMs: number = FACTORY_TIMEOUT_MS
  ) {}

  public async getJson<T>(url: string): Promise<T> {
    const client: AadHttpClient = await this._aad();
    return this._json<T>(this._race(client.get(url, AadHttpClient.configurations.v1)));
  }

  public async postJson<T>(url: string, body: unknown): Promise<T> {
    const client: AadHttpClient = await this._aad();
    const options: IHttpClientOptions = {
      headers: { 'content-type': 'application/json', accept: 'application/json' },
      body: JSON.stringify(body)
    };
    return this._json<T>(this._race(client.post(url, AadHttpClient.configurations.v1, options)));
  }

  private async _aad(): Promise<AadHttpClient> {
    if (!this._client) {
      this._client = this._factory.getClient(this._resource);
    }
    return this._client;
  }

  private async _json<T>(response: Promise<HttpClientResponse>): Promise<T> {
    const settled: HttpClientResponse = await response;
    if (!settled.ok) {
      throw new Error(`Factory answered ${settled.status} for ${settled.url}`);
    }
    return (await settled.json()) as T;
  }

  /** AadHttpClient has no abort signal, so the timeout is a race, not a cancel. */
  private async _race(call: Promise<HttpClientResponse>): Promise<HttpClientResponse> {
    let timer: ReturnType<typeof setTimeout> | undefined;
    const timeout: Promise<never> = new Promise<never>((_resolve, reject) => {
      timer = setTimeout(() => reject(new Error('The factory did not answer in time.')), this._timeoutMs);
    });

    try {
      return await Promise.race([call, timeout]);
    } finally {
      if (timer !== undefined) {
        clearTimeout(timer);
      }
    }
  }
}
