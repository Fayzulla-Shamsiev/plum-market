<script setup lang="ts">
import { ref } from 'vue'
import AutoReplyEditor from '../../components/AutoReplyEditor.vue'
import Modal from '../../components/Modal.vue'

const emit = defineEmits<{ close: []; saved: [] }>()
const editor = ref<InstanceType<typeof AutoReplyEditor> | null>(null)

async function save() {
  await editor.value?.save()
  emit('close')
}
</script>

<template>
  <Modal title="Автоответчик" width="720px" persistent @close="emit('close')">
    <p class="muted intro">
      Сообщение автоматически отправляется клиенту при смене статуса заказа — в чат магазина и в Telegram,
      если у магазина есть бот и клиент его открыл.
    </p>
    <AutoReplyEditor ref="editor" @saved="emit('saved')" />
    <template #footer>
      <button class="btn" @click="emit('close')">Отмена</button>
      <button class="btn btn-primary" @click="save">Сохранить</button>
    </template>
  </Modal>
</template>

<style scoped>
.intro { margin: 0 0 12px; }
</style>
