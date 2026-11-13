import { describe, expect, it } from 'vitest'
import { toMoodInput, toggleMood } from './moodState'

describe('toggleMood', () => {
  it('adds, removes and stops at two', () => {
    expect(toggleMood([], 'cozy')).toEqual(['cozy'])
    expect(toggleMood(['cozy'], 'laugh')).toEqual(['cozy', 'laugh'])
    expect(toggleMood(['cozy', 'laugh'], 'surprise')).toEqual(['cozy', 'laugh'])
    expect(toggleMood(['cozy', 'laugh'], 'cozy')).toEqual(['laugh'])
  })
})

describe('toMoodInput', () => {
  it('sends blank text as null and trims the rest', () => {
    expect(toMoodInput({ moods: ['cozy'], maxRuntime: 'upTo2h', language: 'fr', text: '   ' })).toEqual({
      moods: ['cozy'], maxRuntime: 'upTo2h', language: 'fr', text: null,
    })
    expect(toMoodInput({ moods: [], maxRuntime: 'any', language: null, text: ' funny ' }).text).toBe('funny')
  })
})
