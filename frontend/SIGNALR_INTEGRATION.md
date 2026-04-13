# SignalR Integration Guide

This guide explains how to integrate with the real-time notification system using SignalR.

## Overview

The Gymnastics Platform API exposes a SignalR hub at `/hubs/notifications` that sends real-time notifications to connected clients when domain events occur.

## Connection

### Hub URL

```
ws://localhost:5001/hubs/notifications  (Development)
wss://your-domain.com/hubs/notifications (Production)
```

### Authentication

The SignalR hub requires authentication. Include your JWT access token in the connection:

```typescript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5001/hubs/notifications", {
    accessTokenFactory: () => {
      // Return the user's JWT access token
      return localStorage.getItem("access_token") || "";
    }
  })
  .withAutomaticReconnect()
  .build();
```

## Available Notification Types

### 1. TenantUpdated

Sent when a user's tenant ID changes (e.g., after completing onboarding).

**Event Name:** `TenantUpdated`

**Payload:**
```typescript
{
  Type: "TenantUpdated",
  UserId: string,        // GUID
  NewTenantId: string,   // GUID
  Message: string,
  Timestamp: string      // ISO 8601 datetime
}
```

**Handler Example:**
```typescript
connection.on("TenantUpdated", (notification) => {
  console.log("Tenant updated:", notification);

  // Prompt user to sign out and back in
  showNotification({
    title: "Account Updated",
    message: notification.Message,
    type: "info",
    action: () => signOut()
  });
});
```

### 2. Generic Notification

Generic notifications for future use cases.

**Event Name:** `Notification`

**Payload:**
```typescript
{
  Message: string,
  Data: any,            // Optional additional data
  Timestamp: string     // ISO 8601 datetime
}
```

**Handler Example:**
```typescript
connection.on("Notification", (notification) => {
  console.log("Notification received:", notification);

  showNotification({
    message: notification.Message,
    data: notification.Data
  });
});
```

## Complete React Integration Example

```typescript
import React, { useEffect, useState } from "react";
import * as signalR from "@microsoft/signalr";

interface TenantUpdatedNotification {
  Type: "TenantUpdated";
  UserId: string;
  NewTenantId: string;
  Message: string;
  Timestamp: string;
}

export function useNotifications() {
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl("http://localhost:5001/hubs/notifications", {
        accessTokenFactory: () => localStorage.getItem("access_token") || ""
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    setConnection(newConnection);
  }, []);

  useEffect(() => {
    if (connection) {
      connection.start()
        .then(() => {
          console.log("SignalR Connected");
          setIsConnected(true);
        })
        .catch((err) => console.error("SignalR Connection Error:", err));

      connection.onreconnecting(() => setIsConnected(false));
      connection.onreconnected(() => setIsConnected(true));
      connection.onclose(() => setIsConnected(false));
    }

    return () => {
      connection?.stop();
    };
  }, [connection]);

  const onTenantUpdated = (callback: (notification: TenantUpdatedNotification) => void) => {
    connection?.on("TenantUpdated", callback);
    return () => connection?.off("TenantUpdated", callback);
  };

  return {
    connection,
    isConnected,
    onTenantUpdated
  };
}

// Usage in a component
export function App() {
  const { isConnected, onTenantUpdated } = useNotifications();
  const navigate = useNavigate();

  useEffect(() => {
    const unsubscribe = onTenantUpdated((notification) => {
      // Show notification to user
      toast.info(notification.Message, {
        action: {
          label: "Sign Out",
          onClick: () => {
            // Clear session and redirect to login
            localStorage.removeItem("access_token");
            localStorage.removeItem("refresh_token");
            navigate("/login");
          }
        }
      });
    });

    return unsubscribe;
  }, [onTenantUpdated, navigate]);

  return (
    <div>
      <StatusIndicator connected={isConnected} />
      {/* Your app content */}
    </div>
  );
}
```

## TypeScript Type Definitions

```typescript
// src/types/signalr.ts

export interface TenantUpdatedNotification {
  Type: "TenantUpdated";
  UserId: string;
  NewTenantId: string;
  Message: string;
  Timestamp: string;
}

export interface GenericNotification {
  Message: string;
  Data?: any;
  Timestamp: string;
}

export type Notification = TenantUpdatedNotification | GenericNotification;
```

## Installation

Install the SignalR client library:

```bash
npm install @microsoft/signalr
```

or

```bash
yarn add @microsoft/signalr
```

## Best Practices

1. **Automatic Reconnection:** Always use `.withAutomaticReconnect()` to handle network interruptions
2. **Connection State:** Track connection state and show indicators to users
3. **Error Handling:** Handle connection errors gracefully
4. **Cleanup:** Always stop the connection when component unmounts
5. **Token Refresh:** If your JWT expires, reconnect with a fresh token
6. **User Groups:** Users are automatically added to their personal group (`user:{userId}`) on connection

## Testing

For local development, ensure the API is running on `http://localhost:5001` and that CORS is configured to allow your frontend origin.

## Troubleshooting

### Connection Fails

- Verify the hub URL is correct
- Ensure the JWT token is valid and not expired
- Check CORS configuration allows your frontend origin
- Verify the hub is mapped in `Program.cs` (`app.MapHub<NotificationHub>("/hubs/notifications")`)

### Not Receiving Notifications

- Confirm user is authenticated (check `Context.User` in hub)
- Verify the user ID matches between the notification and the connected user
- Check server logs for errors in the notification handler
- Ensure the event is being published correctly (check domain event interceptor)

## Future Events

Additional notification types will be added as new domain events are implemented:
- `ClubInviteReceived` - When a user receives a club invitation
- `ProgrammeUpdated` - When a training programme is modified
- `SessionBooked` - When a session booking is confirmed
