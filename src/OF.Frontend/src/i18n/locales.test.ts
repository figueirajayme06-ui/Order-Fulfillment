import { describe, expect, it } from "vitest";
import de from "./locales/de.json";
import es from "./locales/es.json";
import fr from "./locales/fr.json";
import italian from "./locales/it.json";

const locales = { de, es, fr, it: italian };

describe("table-column locale content", () => {
  it("contains native Unicode rather than UTF-8 mojibake", () => {
    Object.entries(locales).forEach(([locale, messages]) => {
      expect(JSON.stringify(messages.tableColumns), locale).not.toMatch(/(?:Ã|Â|�|â€™|â€“|â€¦)/u);
    });
  });

  it("preserves representative native translations", () => {
    expect(de.tableColumns.hint).toContain("auswählen");
    expect(es.tableColumns.fields.warehouse).toBe("Almacén");
    expect(fr.tableColumns.requiredReason).toContain("d’identité");
    expect(italian.tableColumns.requiredReason).toContain("è sempre");
  });
});
