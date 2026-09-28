import { describe, it, expect } from 'vitest';
import { parsePower, isControlLocked } from '../../src/devices/application/control-state.js';

// Convergent Testing G0: DEVICES.CONTROL power-off lock rules.
// A device that is committed OFF only accepts the `power` command.

describe('parsePower', () => {
  it.each([[true, true], ['true', true], ['On', true], ['on', true]])('reads %j as on', (input, expected) => {
    expect(parsePower(input)).toBe(expected);
  });

  it.each([[false, false], ['false', false], ['Off', false], ['off', false]])('reads %j as off', (input, expected) => {
    expect(parsePower(input)).toBe(expected);
  });

  it.each([[undefined], [null], [''], ['maybe'], [1]])('reads %j as unknown', (input) => {
    expect(parsePower(input)).toBeNull();
  });
});

describe('isControlLocked', () => {
  it('locks every non-power attribute when the device is off', () => {
    expect(isControlLocked('targetTemperature', false)).toBe(true);
    expect(isControlLocked('mode', false)).toBe(true);
    expect(isControlLocked('brightness', false)).toBe(true);
  });

  it('never locks the power attribute, so the device can be turned back on', () => {
    expect(isControlLocked('power', false)).toBe(false);
    expect(isControlLocked('power', true)).toBe(false);
  });

  it('locks nothing when the device is on', () => {
    expect(isControlLocked('targetTemperature', true)).toBe(false);
  });

  it('locks nothing when the power state is unknown', () => {
    expect(isControlLocked('brightness', null)).toBe(false);
  });
});
