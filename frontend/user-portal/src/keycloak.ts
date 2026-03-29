import Keycloak from 'keycloak-js';

const keycloak = new Keycloak({
  url: import.meta.env.VITE_KEYCLOAK_URL || 'http://localhost:8080',
  realm: import.meta.env.VITE_KEYCLOAK_REALM || 'gymnastics',
  clientId: import.meta.env.VITE_USER_PORTAL_CLIENT_ID || 'user-portal',
});

export default keycloak;
