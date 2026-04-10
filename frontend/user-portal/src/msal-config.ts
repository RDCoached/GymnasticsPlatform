import { Configuration, PopupRequest, RedirectRequest } from '@azure/msal-browser';

const externalIdTenantId = import.meta.env.VITE_EXTERNAL_ID_TENANT_ID || '';
const clientId = import.meta.env.VITE_EXTERNAL_ID_CLIENT_ID || '';
const authority = import.meta.env.VITE_EXTERNAL_ID_AUTHORITY || '';

export const msalConfig: Configuration = {
  auth: {
    clientId,
    authority: `${authority}/${externalIdTenantId}`,
    redirectUri: window.location.origin + '/auth/callback',
    postLogoutRedirectUri: window.location.origin + '/sign-in',
  },
  cache: {
    cacheLocation: 'sessionStorage',
    storeAuthStateInCookie: false,
  },
};

export const loginRequest: PopupRequest = {
  scopes: [`api://${externalIdTenantId}/gymnastics-api/user.access`, 'openid', 'profile', 'email'],
  prompt: 'select_account',
};

export const loginRequestGoogle: RedirectRequest = {
  scopes: [`api://${externalIdTenantId}/gymnastics-api/user.access`, 'openid', 'profile', 'email'],
  domainHint: 'google.com',
  prompt: 'select_account',
};

export const loginRequestMicrosoft: RedirectRequest = {
  scopes: [`api://${externalIdTenantId}/gymnastics-api/user.access`, 'openid', 'profile', 'email'],
  domainHint: 'live.com',
  prompt: 'select_account',
};
