import { describe, expect, it, vi } from 'vitest'
import { uniqueStamp, withoutLongRuns } from '../support/unique-stamp.js'

// Same rule the API applies to client email user names.
const rejectedByApi = /(.)\1{3,}/

describe('test email stamps', () => {
  it.each(['1790000011111', '1111', '17900000000009999', 'abc22223333'])('shortens long runs in %s', (value) => {
    expect(withoutLongRuns(value)).not.toMatch(rejectedByApi)
  })

  it('leaves values without long runs untouched', () => {
    expect(withoutLongRuns('179111222333')).toBe('179111222333')
  })

  it('never produces a rejected stamp, even for a repetitive clock', () => {
    vi.spyOn(Date, 'now').mockReturnValue(1111111111111)

    expect(uniqueStamp()).not.toMatch(rejectedByApi)
    vi.restoreAllMocks()
  })
})
