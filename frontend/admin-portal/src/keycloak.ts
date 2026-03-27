import Keycloak from 'keycloak-js';

const keycloak = new Keycloak({
  url: 'http://localhost:8080',
  realm: 'gymnastics',
  clientId: 'admin-portal',
});

export default keycloak;
