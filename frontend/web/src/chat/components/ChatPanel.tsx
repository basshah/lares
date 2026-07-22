import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useChatHistory, useSendChatMessage } from '../useChat'

export default function ChatPanel() {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const [draft, setDraft] = useState('')
  const { data: messages } = useChatHistory()
  const sendMessage = useSendChatMessage()
  const scrollRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight })
  }, [messages, sendMessage.isPending])

  function handleSend() {
    const text = draft.trim()
    if (!text || sendMessage.isPending) return
    setDraft('')
    sendMessage.mutate(text)
  }

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="fixed bottom-4 right-4 rounded-full bg-indigo-500 hover:bg-indigo-400 text-white w-12 h-12 shadow-lg text-xl"
      >
        {t('chat.toggle')}
      </button>

      {open && (
        <div className="fixed bottom-20 right-4 w-80 h-96 flex flex-col rounded-lg border border-slate-700 bg-slate-900 shadow-xl z-50">
          <div className="px-3 py-2 border-b border-slate-800 text-sm font-semibold">{t('chat.title')}</div>
          <div ref={scrollRef} className="flex-1 overflow-y-auto p-3 flex flex-col gap-2">
            {messages?.map((m) => (
              <div
                key={m.id}
                className={`max-w-[85%] rounded px-3 py-1.5 text-sm whitespace-pre-wrap ${
                  m.role === 'User'
                    ? 'self-end bg-indigo-500/20 text-indigo-100'
                    : 'self-start bg-slate-800 text-slate-100'
                }`}
              >
                {m.content}
              </div>
            ))}
            {sendMessage.isPending && (
              <div className="self-start text-xs text-slate-400">{t('chat.thinking')}</div>
            )}
          </div>
          <div className="p-2 border-t border-slate-800 flex gap-2">
            <input
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleSend()}
              placeholder={t('chat.placeholder')}
              className="flex-1 rounded bg-slate-800 border border-slate-700 px-2 py-1 text-sm outline-none focus:border-indigo-500"
            />
            <button
              type="button"
              onClick={handleSend}
              disabled={sendMessage.isPending}
              className="rounded bg-indigo-500 hover:bg-indigo-400 disabled:opacity-50 px-3 py-1 text-sm text-white"
            >
              {t('chat.send')}
            </button>
          </div>
        </div>
      )}
    </>
  )
}
