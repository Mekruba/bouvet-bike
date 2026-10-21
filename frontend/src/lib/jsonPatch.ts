import type { JsonPatchOperation } from '../api/types.ts'

/**
 * RFC 6902 operations that turn `original` into `edited`, for the given fields only. Unchanged fields are
 * left out, so the server only applies what changed. Empty optional fields become `remove`.
 */
export function diffPatch<T extends object>(
  original: T,
  edited: T,
  fields: Array<keyof T & string>,
  optional: Array<keyof T & string> = [],
): JsonPatchOperation[] {
  const operations: JsonPatchOperation[] = []
  for (const field of fields) {
    const before = original[field]
    const after = edited[field]
    if (normalize(before) === normalize(after)) continue

    const isEmpty = after === null || after === undefined || after === ''
    if (isEmpty && optional.includes(field)) {
      if (!isEmptyValue(before)) operations.push({ op: 'remove', path: `/${field}` })
    } else {
      operations.push({ op: 'replace', path: `/${field}`, value: after })
    }
  }
  return operations
}

const isEmptyValue = (value: unknown) => value === null || value === undefined || value === ''
const normalize = (value: unknown) => (isEmptyValue(value) ? '' : String(value))
