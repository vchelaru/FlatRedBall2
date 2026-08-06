import { test, expect, type Page } from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

/**
 * #690 Phase 1 smoke (A2 assertable labels + B1 AutomationIds).
 *
 * Phase 0 spike (Avalonia.Browser 12.0.1): DOM/CDP ARIA for Avalonia controls is empty
 * (AX tree = page title only). Playwright therefore drives DEBUG `globalThis.__aeUiAutomation`
 * which clicks/dumps by the same AutomationId values set on controls (B1), without shipping
 * FeatureDemos / `?demo=` hooks.
 *
 * Expected undo Descriptions match BrowserUiDriveLabelTests:
 *   Add Frame to 'ColorCycle'
 *   Add Animation 'NewAnimation'
 */

const OUT_DIR = path.join(__dirname, '..', '..', '_out', 'browser-ui-drive');

const EXPECTED_ADD_ANIMATION = "Add Animation 'NewAnimation'";
const EXPECTED_ADD_FRAME = "Add Frame to 'ColorCycle'";

async function waitForEditorReady(page: Page): Promise<void> {
  await page.waitForFunction(() => document.querySelectorAll('canvas').length >= 1, null, {
    timeout: 90_000,
  });
  await page.waitForFunction(() => !!(globalThis as any).__aeUiAutomation, null, {
    timeout: 60_000,
  });
}

async function collectAxNames(page: Page): Promise<string[]> {
  const client = await page.context().newCDPSession(page);
  const { nodes } = await client.send('Accessibility.getFullAXTree');
  const names: string[] = [];
  for (const node of nodes ?? []) {
    const nameProp = node.name?.value ?? node.name;
    if (typeof nameProp === 'string' && nameProp.trim().length > 0) names.push(nameProp.trim());
  }
  return names;
}

async function aeClick(page: Page, automationId: string): Promise<boolean> {
  return page.evaluate((id) => {
    const bridge = (globalThis as any).__aeUiAutomation;
    if (!bridge) return false;
    return !!bridge.clickByAutomationId(id);
  }, automationId);
}

async function aeDumpUndo(page: Page): Promise<string[]> {
  const json = await page.evaluate(() => {
    const bridge = (globalThis as any).__aeUiAutomation;
    if (!bridge) return '[]';
    return bridge.dumpUndoDescriptionsJson();
  });
  return JSON.parse(json) as string[];
}

test.describe('Browser UI-drive History (#690)', () => {
  test('Add Animation + Add Frame → History lists assertable undo labels', async ({ page }) => {
    fs.mkdirSync(OUT_DIR, { recursive: true });

    await page.goto('/');
    await waitForEditorReady(page);

    // Phase 0 evidence: DOM ARIA stays empty even after AutomationProperties are set.
    const axBefore = await collectAxNames(page);
    fs.writeFileSync(path.join(OUT_DIR, 'ax-tree-before.json'), JSON.stringify(axBefore, null, 2));
    fs.writeFileSync(
      path.join(OUT_DIR, 'phase0-findings.md'),
      [
        '# #690 Phase 0 — Avalonia.Browser 12.0.1 a11y spike',
        '',
        '- CDP `Accessibility.getFullAXTree` names after load: see `ax-tree-before.json`.',
        '- Observed: only the document title appears — **no** History / Add Animation / tree peers.',
        '- DOM `aria-*` on Avalonia controls: none (canvas host only).',
        '- Verdict: **B1 DOM/ARIA for Playwright getByRole = NO-GO** on this Avalonia version.',
        '- Phase 1 path: DEBUG `__aeUiAutomation` bridge keyed by B1 `AutomationId` values + A2 dump of undo Descriptions.',
        '',
      ].join('\n')
    );

    expect(await aeClick(page, 'add-frame'), 'click add-frame').toBeTruthy();
    await page.waitForTimeout(400);
    expect(await aeClick(page, 'add-animation'), 'click add-animation').toBeTruthy();
    await page.waitForTimeout(400);
    expect(await aeClick(page, 'history-tab'), 'open History tab').toBeTruthy();
    await page.waitForTimeout(600);

    const descriptions = await aeDumpUndo(page);
    fs.writeFileSync(path.join(OUT_DIR, 'undo-descriptions.json'), JSON.stringify(descriptions, null, 2));

    await page.screenshot({ path: path.join(OUT_DIR, 'history-after-ui-drive.png'), fullPage: true });

    expect(descriptions).toContain(EXPECTED_ADD_ANIMATION);
    expect(descriptions).toContain(EXPECTED_ADD_FRAME);
  });
});
