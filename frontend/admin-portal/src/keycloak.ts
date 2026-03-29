import Keycloak from 'keycloak-js';

const keycloak = new Keycloak({
  url: import.meta.env.VITE_KEYCLOAK_URL || 'http://localhost:8080',
  realm: import.meta.env.VITE_KEYCLOAK_REALM || 'gymnastics',
  clientId: import.meta.env.VITE_ADMIN_PORTAL_CLIENT_ID || 'admin-portal',
});

export default keycloak;
