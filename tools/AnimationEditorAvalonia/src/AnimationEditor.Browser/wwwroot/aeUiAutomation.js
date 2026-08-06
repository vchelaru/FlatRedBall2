// DEBUG harness for #690 Playwright UI-drive. Registers globalThis.__aeUiAutomation so the
// external runner can click by AutomationId and dump undo Descriptions without DOM ARIA
// (Avalonia.Browser does not emit control ARIA today).
export function register(clickById, dumpUndoJson) {
  globalThis.__aeUiAutomation = {
    clickByAutomationId: (id) => clickById(id),
    dumpUndoDescriptionsJson: () => dumpUndoJson(),
  };
}
