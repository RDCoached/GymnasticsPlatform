import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { ReactKeycloakProvider } from '@react-keycloak/web'
import keycloak from './keycloak'
import './index.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ReactKeycloakProvider authClient={keycloak}>
      <App />
    </ReactKeycloakProvider>
  </StrictMode>,
)
