import { describe, it, expect } from 'vitest'
import { useConfirm } from '@/composables/useConfirm'

describe('useConfirm.js', () => {
  it('opens confirm modal with specified parameters', () => {
    const { confirm, isVisible, confirmTitle, confirmMessage, confirmOkText, confirmCancelText } = useConfirm()

    confirm({
      title: 'Eliminar Registro',
      message: '¿Está seguro de eliminar este elemento?',
      okText: 'Sí, eliminar',
      cancelText: 'No, conservar',
    })

    expect(isVisible.value).toBe(true)
    expect(confirmTitle.value).toBe('Eliminar Registro')
    expect(confirmMessage.value).toBe('¿Está seguro de eliminar este elemento?')
    expect(confirmOkText.value).toBe('Sí, eliminar')
    expect(confirmCancelText.value).toBe('No, conservar')
  })

  it('resolves promise to true when handleOk is called', async () => {
    const { confirm, handleOk, isVisible } = useConfirm()

    const promise = confirm({ message: 'Confirmar' })
    expect(isVisible.value).toBe(true)

    handleOk()
    const result = await promise
    expect(result).toBe(true)
    expect(isVisible.value).toBe(false)
  })

  it('resolves promise to false when handleCancel is called', async () => {
    const { confirm, handleCancel, isVisible } = useConfirm()

    const promise = confirm({ message: 'Cancelar' })
    expect(isVisible.value).toBe(true)

    handleCancel()
    const result = await promise
    expect(result).toBe(false)
    expect(isVisible.value).toBe(false)
  })
})
