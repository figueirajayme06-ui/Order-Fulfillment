import { createRoot } from "react-dom/client";
import { App } from "./App";
import "./i18n";
import "./theme.css";
import "./index.css";
import "./print.css";

const rootElement = document.getElementById("root");
if (!rootElement) {
  throw new Error("Unable to find root element with id = 'root'");
}

createRoot(rootElement).render(<App />);
