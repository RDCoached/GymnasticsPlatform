import { Configuration, PopupRequest } from '@azure/msal-browser';

export const msalConfig: Configuration = {
  auth: {
    clientId: import.meta.env.VITE_ENTRA_CLIENT_ID || '',
    authority: `https://login.microsoftonline.com/${import.meta.env.VITE_ENTRA_TENANT_ID || 'common'}`,
    redirectUri: import.meta.env.VITE_REDIRECT_URI || window.location.origin + '/auth/callback',
    postLogoutRedirectUri: window.location.origin + '/sign-in',
    navigateToLoginRequestUrl: true,
  },
  cache: {
    cacheLocation: 'sessionStorage',
    storeAuthStateInCookie: false,
  },
  system: {
    allowNativeBroker: false,
    loggerOptions: {
      logLevel: import.meta.env.DEV ? 3 : 1,
      loggerCallback: (level, message, containsPii) => {
        if (!containsPii) {
          console.log(message);
        }
      },
    },
  },
};

export const loginRequest: PopupRequest = {
  scopes: ['api://gymnastics-api/user.access', 'openid', 'profile', 'email'],
  prompt: 'select_account',
};

export const googleLoginRequest: PopupRequest = {
  ...loginRequest,
  domainHint: 'google',
};

export const microsoftLoginRequest: PopupRequest = {
  ...loginRequest,
  domainHint: 'consumers',
};
