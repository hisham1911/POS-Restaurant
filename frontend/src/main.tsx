import React from "react";
import ReactDOM from "react-dom/client";
import { Provider } from "react-redux";
import { PersistGate } from "redux-persist/integration/react";
import { Toaster } from "sonner";

import { store, persistor } from "./store";
import App from "./App";
import { TaxSettingsSync } from "./components/common/TaxSettingsSync";

// Import Cairo font from @fontsource (Windows 7 compatible)
import "@fontsource/cairo/400.css";
import "@fontsource/cairo/500.css";
import "@fontsource/cairo/600.css";
import "@fontsource/cairo/700.css";

import "./index.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <Provider store={store}>
      <PersistGate loading={null} persistor={persistor}>
        <TaxSettingsSync />
        <App />
        <Toaster
          position="top-center"
          dir="rtl"
          richColors
          toastOptions={{
            duration: 3000,
            style: {
              fontFamily: "Cairo, sans-serif",
              direction: "rtl",
              textAlign: "right",
              padding: "16px 20px",
              fontSize: "16px",
              minWidth: "320px",
            },
            classNames: {
              toast: "!bg-white !border !border-gray-200 !shadow-lg !rounded-xl !py-4 !px-5",
              title: "!text-gray-800 !font-semibold !text-base",
              description: "!text-gray-500 !text-sm",
              success: "!bg-success-50 !border-success-200 !text-success-700",
              error: "!bg-danger-50 !border-danger-200 !text-danger-700",
              warning: "!bg-warning-50 !border-warning-200 !text-warning-700",
              info: "!bg-info-50 !border-primary-200 !text-primary-700",
            },
          }}
        />
      </PersistGate>
    </Provider>
  </React.StrictMode>
);
