/**
 * #761 — Skia/Avalonia probes UNMASKED_VENDOR/RENDERER via getParameter without
 * enabling WEBGL_debug_renderer_info first. WebKit logs:
 *   WebGL: INVALID_ENUM: getParameter: invalid parameter name,
 *   WEBGL_debug_renderer_info not enabled
 * This unit-tests the early wwwroot patch that enables the extension (or falls
 * back to masked VENDOR/RENDERER) before those probes run.
 */
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PATCH_PATH = path.resolve(
  __dirname,
  '../../../src/AnimationEditor.Browser/wwwroot/webglDebugRendererInfoPatch.js'
);

const UNMASKED_VENDOR_WEBGL = 0x9245;
const UNMASKED_RENDERER_WEBGL = 0x9246;
const GL_VENDOR = 0x1f00;
const GL_RENDERER = 0x1f01;

function loadPatchApi() {
  const code = fs.readFileSync(PATCH_PATH, 'utf8');
  const sandbox = { console };
  sandbox.globalThis = sandbox;
  vm.runInNewContext(code, sandbox, { filename: 'webglDebugRendererInfoPatch.js' });
  const api = sandbox.__frbWebglDebugRendererInfoPatch;
  assert.ok(api, 'patch script must export globalThis.__frbWebglDebugRendererInfoPatch');
  return api;
}

function makeFakeGl({ extensionAvailable }) {
  const calls = { getExtension: [], getParameter: [] };
  const gl = {
    VENDOR: GL_VENDOR,
    RENDERER: GL_RENDERER,
    getExtension(name) {
      calls.getExtension.push(name);
      if (name === 'WEBGL_debug_renderer_info' && extensionAvailable) {
        return {
          UNMASKED_VENDOR_WEBGL,
          UNMASKED_RENDERER_WEBGL,
        };
      }
      return null;
    },
    getParameter(pname) {
      calls.getParameter.push(pname);
      if (pname === UNMASKED_VENDOR_WEBGL || pname === UNMASKED_RENDERER_WEBGL) {
        // Mimic WebKit: probing these enums without enabling the extension is invalid.
        if (!extensionAvailable || !calls.getExtension.includes('WEBGL_debug_renderer_info')) {
          throw new Error('WEBGL_debug_renderer_info not enabled');
        }
        return pname === UNMASKED_VENDOR_WEBGL ? 'Unmasked Vendor' : 'Unmasked Renderer';
      }
      if (pname === GL_VENDOR) return 'Masked Vendor';
      if (pname === GL_RENDERER) return 'Masked Renderer';
      return null;
    },
  };
  return { gl, calls };
}

test('createSafeGetParameter enables WEBGL_debug_renderer_info before UNMASKED probes', () => {
  const api = loadPatchApi();
  const { gl, calls } = makeFakeGl({ extensionAvailable: true });
  const safe = api.createSafeGetParameter(gl.getParameter, gl.getExtension);

  assert.equal(safe.call(gl, UNMASKED_VENDOR_WEBGL), 'Unmasked Vendor');
  assert.equal(safe.call(gl, UNMASKED_RENDERER_WEBGL), 'Unmasked Renderer');
  assert.deepEqual(calls.getExtension, [
    'WEBGL_debug_renderer_info',
    'WEBGL_debug_renderer_info',
  ]);
});

test('createSafeGetParameter falls back to masked VENDOR/RENDERER when extension missing', () => {
  const api = loadPatchApi();
  const { gl, calls } = makeFakeGl({ extensionAvailable: false });
  const safe = api.createSafeGetParameter(gl.getParameter, gl.getExtension);

  assert.equal(safe.call(gl, UNMASKED_VENDOR_WEBGL), 'Masked Vendor');
  assert.equal(safe.call(gl, UNMASKED_RENDERER_WEBGL), 'Masked Renderer');
  assert.ok(!calls.getParameter.includes(UNMASKED_VENDOR_WEBGL));
  assert.ok(!calls.getParameter.includes(UNMASKED_RENDERER_WEBGL));
  assert.deepEqual(calls.getParameter, [GL_VENDOR, GL_RENDERER]);
});

test('install patches WebGLRenderingContext and WebGL2RenderingContext prototypes', () => {
  const api = loadPatchApi();
  const webglProto = {
    getParameter() {
      return 'raw';
    },
    getExtension() {
      return null;
    },
  };
  const webgl2Proto = {
    getParameter() {
      return 'raw2';
    },
    getExtension() {
      return null;
    },
  };
  const g = {
    WebGLRenderingContext: { prototype: webglProto },
    WebGL2RenderingContext: { prototype: webgl2Proto },
  };

  api.install(g);

  assert.notEqual(webglProto.getParameter, undefined);
  assert.equal(webglProto.__frbWebglDebugRendererPatched, true);
  assert.equal(webgl2Proto.__frbWebglDebugRendererPatched, true);

  // Missing extension → masked fallback path must not throw.
  webglProto.VENDOR = GL_VENDOR;
  webglProto.RENDERER = GL_RENDERER;
  const original = webglProto.getParameter;
  // Re-bind a real original under the patch: install already wrapped once; call through.
  assert.doesNotThrow(() => webglProto.getParameter(UNMASKED_VENDOR_WEBGL));
  void original;
});
