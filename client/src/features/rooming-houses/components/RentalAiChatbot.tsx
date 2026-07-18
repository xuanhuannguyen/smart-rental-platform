import { useEffect, useMemo, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAssetUrl } from '../../../shared/api/assets';
import { contactLandlord } from '../../chat/api';
import { chatRoomingHouseAssistant } from '../api';
import type { NearbyPlace, RoomingHouseAiChatResponse, RoomingHouseSearchItem } from '../types';
import './RentalAiChatbot.css';

type ChatContext = 'home' | 'search' | 'detail';

type RentalAiChatbotProps = {
  context: ChatContext;
  roomingHouseId?: string;
  title?: string;
};

type ChatMessage = {
  id: string;
  role: 'assistant' | 'user';
  text: string;
  response?: RoomingHouseAiChatResponse;
};

type PersistedChatSession = {
  conversationId: string | null;
  messages: ChatMessage[];
  updatedAt: number;
};

const CHAT_SESSION_STORAGE_KEY = 'smartRental.aiChat.session';
const CHAT_SESSION_TTL_MS = 30 * 60 * 1000;
const MAX_PERSISTED_MESSAGES = 30;

const QUICK_PROMPTS: Record<ChatContext, string[]> = {
  home: [
    'Tìm trọ gần Đại học FPT Đà Nẵng',
    'Tư vấn khu trọ giá dưới 3 triệu',
    'Khu nào còn nhiều phòng trống?'
  ],
  search: [
    'Lọc giúp tôi khu trọ phù hợp sinh viên',
    'Có khu nào giá tốt và gần trung tâm không?',
    'Gợi ý khu trọ có wifi và giữ xe'
  ],
  detail: [
    'Chính sách cọc và thanh toán thế nào?',
    'Nội quy khách qua đêm và giữ xe ra sao?',
    'Xung quanh có quán ăn, chợ hoặc trường học nào?'
  ]
};

export default function RentalAiChatbot({ context, roomingHouseId, title }: RentalAiChatbotProps) {
  const initialSession = useMemo(() => loadChatSession(context, title), [context, title]);
  const [open, setOpen] = useState(false);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [conversationId, setConversationId] = useState<string | null>(initialSession.conversationId);
  const [messages, setMessages] = useState<ChatMessage[]>(initialSession.messages);
  const [contactingLandlord, setContactingLandlord] = useState(false);
  const inputRef = useRef<HTMLInputElement | null>(null);
  const messagesEndRef = useRef<HTMLDivElement | null>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    if (open) {
      scrollToBottom();
      const t1 = window.setTimeout(scrollToBottom, 100);
      const t2 = window.setTimeout(scrollToBottom, 400);
      return () => {
        window.clearTimeout(t1);
        window.clearTimeout(t2);
      };
    }
  }, [open, messages, loading]);

  const quickPrompts = useMemo(() => QUICK_PROMPTS[context], [context]);

  async function submitMessage(message: string) {
    const trimmed = message.trim();
    if (!trimmed || loading) return;

    const previousMessages = messages;
    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      text: trimmed
    };

    let messagesWithUser: ChatMessage[] = [];
    setMessages((current) => {
      messagesWithUser = [...current, userMessage];
      saveChatSession(conversationId, messagesWithUser);
      return messagesWithUser;
    });
    setInput('');
    setLoading(true);

    try {
      const response = await chatRoomingHouseAssistant({
        message: trimmed,
        context,
        roomingHouseId: roomingHouseId ?? null,
        mode: 'detailed',
        conversationId: conversationId ?? null,
        chatHistory: buildChatHistory(previousMessages)
      });

      const nextConversationId = response.conversationId || conversationId;
      if (nextConversationId && nextConversationId !== conversationId) {
        setConversationId(nextConversationId);
      }

      const assistantMessage = createAssistantMessage(response);
      setMessages((current) => [...current, assistantMessage]);
      saveChatSession(nextConversationId, [
        ...messagesWithUser,
        assistantMessage
      ]);
    } catch (error) {
      const errorMessage: ChatMessage = {
          id: crypto.randomUUID(),
          role: 'assistant',
          text: getApiErrorMessage(error, 'Mình chưa thể gọi chatbot lúc này. Bạn thử lại sau nhé.')
        };
      setMessages((current) => {
        const nextMessages = [...current, errorMessage];
        saveChatSession(conversationId, nextMessages);
        return nextMessages;
      });
    } finally {
      setLoading(false);
      window.setTimeout(() => inputRef.current?.focus(), 0);
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void submitMessage(input);
  }

  async function handleContactLandlord() {
    if (!roomingHouseId || contactingLandlord) return;

    const content = input.trim() || `Em muốn hỏi thêm về ${title || 'khu trọ này'}. Chủ trọ tư vấn giúp em với ạ.`;
    setContactingLandlord(true);
    try {
      const conversation = await contactLandlord(roomingHouseId, content);
      window.dispatchEvent(new CustomEvent('open-chat-bubble', {
        detail: { conversationId: conversation.id }
      }));
      window.dispatchEvent(new CustomEvent('refresh-chat-list'));
      setInput('');
      const assistantMessage: ChatMessage = {
        id: crypto.randomUUID(),
        role: 'assistant',
        text: 'Mình đã gửi tin nhắn này cho chủ trọ. Bạn có thể tiếp tục trao đổi trong khung chat.'
      };
      setMessages(current => {
        const nextMessages = [...current, assistantMessage];
        saveChatSession(conversationId, nextMessages);
        return nextMessages;
      });
    } catch (error) {
      const assistantMessage: ChatMessage = {
        id: crypto.randomUUID(),
        role: 'assistant',
        text: getApiErrorMessage(error, 'Chưa gửi được tin nhắn cho chủ trọ. Bạn thử lại sau nhé.')
      };
      setMessages(current => {
        const nextMessages = [...current, assistantMessage];
        saveChatSession(conversationId, nextMessages);
        return nextMessages;
      });
    } finally {
      setContactingLandlord(false);
    }
  }

  return (
    <div className={`rental-ai-chatbot ${open ? 'rental-ai-chatbot--open' : ''}`}>
      {open && (
        <section className="rental-ai-chatbot__panel" aria-label="Chatbot tư vấn tìm trọ">
          <header className="rental-ai-chatbot__header">
            <div className="rental-ai-chatbot__header-title-container">
              <div className="rental-ai-chatbot__header-icon-badge">
                <DropletSparkleIcon />
              </div>
              <div className="rental-ai-chatbot__header-text">
                <span className="rental-ai-chatbot__eyebrow">AI TƯ VẤN</span>
                <h2>{context === 'detail' ? 'Hỏi về khu trọ này' : 'Tìm trọ thông minh'}</h2>
              </div>
            </div>
            <button type="button" className="rental-ai-chatbot__close" onClick={() => setOpen(false)} aria-label="Đóng chatbot">
              <CloseIcon />
            </button>
          </header>

          <div className="rental-ai-chatbot__messages">
            {messages.map((message) => {
              const isAssistant = message.role === 'assistant';
              return (
                <div key={message.id} className={`rental-ai-chatbot__message-wrapper rental-ai-chatbot__message-wrapper--${message.role}`}>
                  {isAssistant && <RobotAvatar className="rental-ai-chatbot__avatar" />}
                  <article className={`rental-ai-chatbot__message rental-ai-chatbot__message--${message.role}`}>
                    <div className="rental-ai-chatbot__message-content">
                      <FormattedChatText text={message.text} />
                      {message.response && <ChatResponseBlocks response={message.response} onFollowUp={submitMessage} loading={loading} />}
                    </div>
                    {isAssistant && (
                      <span className="rental-ai-chatbot__timestamp">
                        {message.id === 'welcome' ? '10:30' : getCurrentTime()}
                      </span>
                    )}
                  </article>
                </div>
              );
            })}
            {messages.length === 1 && messages[0].id === 'welcome' && !loading && (
              <div className="rental-ai-chatbot__empty-state">
                <HouseIllustration className="rental-ai-chatbot__empty-illustration" />
                <p>Hãy cho mình biết bạn đang tìm<br />khu trọ như thế nào nhé!</p>
              </div>
            )}
            {loading && (
              <div className="rental-ai-chatbot__message-wrapper rental-ai-chatbot__message-wrapper--assistant">
                <RobotAvatar className="rental-ai-chatbot__avatar" />
                <article className="rental-ai-chatbot__message rental-ai-chatbot__message--assistant rental-ai-chatbot__message--loading">
                  <div className="rental-ai-chatbot__loading-dots">
                    <span></span>
                    <span></span>
                    <span></span>
                  </div>
                </article>
              </div>
            )}
            <div ref={messagesEndRef} />
          </div>

          <div className="rental-ai-chatbot__quick">
            {quickPrompts.map((prompt) => (
              <button
                key={prompt}
                type="button"
                className="rental-ai-chatbot__quick-pill"
                onClick={() => void submitMessage(prompt)}
                disabled={loading}
              >
                {getPromptIcon(prompt)}
                <span>{prompt}</span>
              </button>
            ))}
          </div>

          <form className="rental-ai-chatbot__form" onSubmit={handleSubmit}>
            {context === 'detail' && roomingHouseId && (
              <button
                type="button"
                className="rental-ai-chatbot__contact-landlord"
                onClick={() => void handleContactLandlord()}
                disabled={loading || contactingLandlord}
              >
                {contactingLandlord ? 'Đang gửi cho chủ trọ...' : 'Nhắn chủ trọ'}
              </button>
            )}
            <div className="rental-ai-chatbot__input-group">
              <button type="button" className="rental-ai-chatbot__input-icon-btn" aria-label="Biểu cảm">
                <SmileyIcon />
              </button>
              <input
                ref={inputRef}
                value={input}
                onChange={(event) => setInput(event.target.value)}
                placeholder={context === 'detail' ? 'Hỏi chính sách, phòng, xung quanh...' : 'Bạn muốn tìm trọ như thế nào?'}
                disabled={loading}
              />
              <button type="button" className="rental-ai-chatbot__input-icon-btn" aria-label="Đính kèm">
                <ClipIcon />
              </button>
            </div>
            <button type="submit" className="rental-ai-chatbot__send-btn" disabled={loading || !input.trim()} aria-label="Gửi câu hỏi">
              <span>Gửi</span>
              <SparkleIcon size={12} className="rental-ai-chatbot__send-sparkle" />
            </button>
          </form>
        </section>
      )}

      <button type="button" className="rental-ai-chatbot__launcher" onClick={() => setOpen((value) => !value)}>
        <span className="rental-ai-chatbot__launcher-badge">AI</span>
        <strong>Tư vấn trọ</strong>
        <SparkleIcon size={14} className="rental-ai-chatbot__launcher-sparkle" />
      </button>
    </div>
  );
}

function createAssistantMessage(response: RoomingHouseAiChatResponse): ChatMessage {
  return {
    id: crypto.randomUUID(),
    role: 'assistant',
    text: response.reply,
    response
  };
}

function loadChatSession(context: ChatContext, title?: string): PersistedChatSession {
  const fallback = createInitialChatSession(context, title);

  if (typeof window === 'undefined') {
    return fallback;
  }

  try {
    const raw = window.localStorage.getItem(CHAT_SESSION_STORAGE_KEY);
    if (!raw) {
      return fallback;
    }

    const parsed = JSON.parse(raw) as Partial<PersistedChatSession>;
    if (!parsed.updatedAt || Date.now() - parsed.updatedAt > CHAT_SESSION_TTL_MS) {
      window.localStorage.removeItem(CHAT_SESSION_STORAGE_KEY);
      return fallback;
    }

    const messages = normalizePersistedMessages(parsed.messages);
    if (messages.length === 0) {
      return fallback;
    }

    return {
      conversationId: typeof parsed.conversationId === 'string' ? parsed.conversationId : null,
      messages,
      updatedAt: parsed.updatedAt
    };
  } catch {
    window.localStorage.removeItem(CHAT_SESSION_STORAGE_KEY);
    return fallback;
  }
}

function createInitialChatSession(context: ChatContext, title?: string): PersistedChatSession {
  return {
    conversationId: null,
    messages: [
      {
        id: 'welcome',
        role: 'assistant',
        text: getWelcomeMessage(context, title)
      }
    ],
    updatedAt: Date.now()
  };
}

function normalizePersistedMessages(value: unknown): ChatMessage[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value
    .filter((item): item is ChatMessage => {
      const candidate = item as Partial<ChatMessage>;
      return (
        typeof candidate.id === 'string' &&
        (candidate.role === 'assistant' || candidate.role === 'user') &&
        typeof candidate.text === 'string'
      );
    })
    .slice(-MAX_PERSISTED_MESSAGES);
}

function saveChatSession(conversationId: string | null, messages: ChatMessage[]) {
  if (typeof window === 'undefined') {
    return;
  }

  const payload: PersistedChatSession = {
    conversationId,
    messages: messages.slice(-MAX_PERSISTED_MESSAGES),
    updatedAt: Date.now()
  };

  window.localStorage.setItem(CHAT_SESSION_STORAGE_KEY, JSON.stringify(payload));
}

function buildChatHistory(messages: ChatMessage[]) {
  return messages
    .filter((message) => message.id !== 'welcome')
    .map((message) => ({
      role: message.role,
      text: message.text.trim()
    }))
    .filter((message) => message.text.length > 0)
    .slice(-12);
}

function FormattedChatText({ text }: { text: string }) {
  const blocks = formatChatText(text);

  return (
    <div className="rental-ai-chatbot__formatted-text">
      {blocks.map((block, index) => renderChatTextBlock(block, index))}
    </div>
  );
}

type ChatTextBlock =
  | { type: 'paragraph'; text: string }
  | { type: 'orderedList'; items: string[] }
  | { type: 'unorderedList'; items: string[] };

function formatChatText(text: string): ChatTextBlock[] {
  const normalized = text
    .replace(/\r\n/g, '\n')
    .replace(/\s+(\d+)\.\s+/g, '\n$1. ')
    .replace(/\s+[-•]\s+/g, '\n- ')
    .replace(/\s+(Bạn có thể xem|Bạn có thể hỏi|Gợi ý|Lưu ý|Mình đã đặt)/g, '\n$1')
    .trim();

  const lines = normalized
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean);

  const blocks: ChatTextBlock[] = [];
  let orderedItems: string[] = [];
  let unorderedItems: string[] = [];

  function flushLists() {
    if (orderedItems.length > 0) {
      blocks.push({ type: 'orderedList', items: orderedItems });
      orderedItems = [];
    }

    if (unorderedItems.length > 0) {
      blocks.push({ type: 'unorderedList', items: unorderedItems });
      unorderedItems = [];
    }
  }

  for (const line of lines) {
    const orderedMatch = line.match(/^\d+\.\s*(.+)$/);
    if (orderedMatch) {
      if (unorderedItems.length > 0) {
        blocks.push({ type: 'unorderedList', items: unorderedItems });
        unorderedItems = [];
      }
      orderedItems.push(orderedMatch[1]);
      continue;
    }

    const unorderedMatch = line.match(/^[-•]\s*(.+)$/);
    if (unorderedMatch) {
      if (orderedItems.length > 0) {
        blocks.push({ type: 'orderedList', items: orderedItems });
        orderedItems = [];
      }
      unorderedItems.push(unorderedMatch[1]);
      continue;
    }

    flushLists();
    blocks.push({ type: 'paragraph', text: line });
  }

  flushLists();
  return blocks.length > 0 ? blocks : [{ type: 'paragraph', text }];
}

function renderChatTextBlock(block: ChatTextBlock, index: number): ReactNode {
  if (block.type === 'orderedList') {
    return (
      <ol key={`ordered-${index}`} className="rental-ai-chatbot__answer-list rental-ai-chatbot__answer-list--ordered">
        {block.items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ol>
    );
  }

  if (block.type === 'unorderedList') {
    return (
      <ul key={`unordered-${index}`} className="rental-ai-chatbot__answer-list">
        {block.items.map((item) => (
          <li key={item}>{item}</li>
        ))}
      </ul>
    );
  }

  return <p key={`paragraph-${index}`}>{block.text}</p>;
}

function ChatResponseBlocks({
  response,
  onFollowUp,
  loading
}: {
  response: RoomingHouseAiChatResponse;
  onFollowUp: (question: string) => Promise<void>;
  loading: boolean;
}) {
  return (
    <>
      {response.roomingHouses.length > 0 && (
        <div className="rental-ai-chatbot__cards">
          {response.roomingHouses.slice(0, 4).map((house) => (
            <RoomingHouseMiniCard key={house.id} house={house} />
          ))}
        </div>
      )}

      {response.nearbyPlaces.length > 0 && (
        <div className="rental-ai-chatbot__nearby">
          {response.nearbyPlaces.slice(0, 6).map((place) => (
            <NearbyPlaceItem key={`${place.name}-${place.displayAddress ?? place.address ?? ''}`} place={place} />
          ))}
        </div>
      )}

      {response.followUpQuestions.length > 0 && (
        <div className="rental-ai-chatbot__followups">
          {response.followUpQuestions.slice(0, 3).map((question) => (
            <button key={question} type="button" onClick={() => void onFollowUp(question)} disabled={loading}>
              {question}
            </button>
          ))}
        </div>
      )}
    </>
  );
}

function RoomingHouseMiniCard({ house }: { house: RoomingHouseSearchItem }) {
  const imageUrl = house.coverImageUrl ? toAssetUrl(house.coverImageUrl) : '';
  return (
    <Link className="rental-ai-chatbot__house" to={`/rooming-houses/${house.id}`}>
      <div className="rental-ai-chatbot__house-image">
        {imageUrl ? <img src={imageUrl} alt={house.name} /> : <span>Trọ</span>}
      </div>
      <div>
        <strong>{house.name}</strong>
        <small>{house.addressDisplay}</small>
        {house.distanceKm != null && (
          <span className="rental-ai-chatbot__distance">Cách khoảng {formatDistance(house.distanceKm)}</span>
        )}
        <em>{formatHouseMeta(house)}</em>
      </div>
    </Link>
  );
}

function NearbyPlaceItem({ place }: { place: NearbyPlace }) {
  return (
    <div className="rental-ai-chatbot__place">
      <strong>{place.name}</strong>
      <small>{place.displayAddress || place.address || 'Chưa có địa chỉ chi tiết'}</small>
      {place.distanceKm != null && <em>Cách khoảng {formatDistance(place.distanceKm)}</em>}
    </div>
  );
}

function getWelcomeMessage(context: ChatContext, title?: string) {
  if (context === 'detail') {
    return `Mình có thể trả lời chi tiết về ${title ?? 'khu trọ này'}: phòng trống, giá, chính sách, nội quy và địa điểm xung quanh.`;
  }

  if (context === 'search') {
    return 'Mình có thể giúp bạn lọc và so sánh các khu trọ trong danh sách hiện tại.';
  }

  return 'Bạn cứ hỏi bằng ngôn ngữ tự nhiên, mình sẽ tìm khu trọ phù hợp và trả về thẻ kết quả để xem nhanh.';
}

function formatHouseMeta(house: RoomingHouseSearchItem) {
  const price = formatPriceRange(house.minMonthlyRent, house.maxMonthlyRent);
  const rooms = `${house.availableRooms} phòng trống`;
  return `${rooms}${price ? ` · ${price}` : ''}`;
}

function formatPriceRange(min?: number | null, max?: number | null) {
  if (min == null && max == null) return '';
  if (min != null && max != null && min !== max) {
    return `${formatCurrencyCompact(min)} - ${formatCurrencyCompact(max)}`;
  }
  return `Từ ${formatCurrencyCompact(min ?? max ?? 0)}`;
}

function formatCurrencyCompact(value: number) {
  if (value >= 1_000_000) {
    return `${new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 }).format(value / 1_000_000)} triệu`;
  }
  return new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 }).format(value);
}

function formatDistance(value: number) {
  if (value < 1) {
    return `${Math.round(value * 1000)} m`;
  }

  return `${new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 1 }).format(value)} km`;
}

// ==========================================
// NEW SVG ICONS & HELPER FUNCTIONS FOR MOCKUP
// ==========================================

function getCurrentTime() {
  const now = new Date();
  return now.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit', hour12: false });
}

function getPromptIcon(prompt: string) {
  if (prompt.toLowerCase().includes('lọc')) {
    return <FilterIcon className="rental-ai-chatbot__quick-icon" />;
  }
  if (prompt.toLowerCase().includes('giá tốt') || prompt.toLowerCase().includes('gần')) {
    return <MapPinIcon className="rental-ai-chatbot__quick-icon" />;
  }
  return <SparkleIcon className="rental-ai-chatbot__quick-icon" size={14} />;
}

const SparkleIcon = ({ className, size = 16 }: { className?: string; size?: number }) => (
  <svg className={className} width={size} height={size} viewBox="0 0 24 24" fill="currentColor">
    <path d="M12 0L14.6 9.4L24 12L14.6 14.6L12 24L9.4 14.6L0 12L9.4 9.4L12 0Z" />
  </svg>
);

const DropletSparkleIcon = ({ className }: { className?: string }) => (
  <svg className={className} viewBox="0 0 24 24" fill="currentColor" width="24" height="24">
    <path d="M12 2.69l5.66 5.66a8 8 0 1 1-11.31 0z" />
    <path d="M19 3l.8 1.7L21.5 5.5l-1.7.8-.8 1.7-.8-1.7-1.7-.8 1.7-.8z" />
    <path d="M5 8l.5 1.2 1.2.5-1.2.5-.5 1.2-.5-1.2-1.2-.5 1.2-.5z" />
  </svg>
);

const CloseIcon = ({ className }: { className?: string }) => (
  <svg className={className} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" width="14" height="14">
    <line x1="18" y1="6" x2="6" y2="18" />
    <line x1="6" y1="6" x2="18" y2="18" />
  </svg>
);

const RobotAvatar = ({ className }: { className?: string }) => (
  <svg className={className} viewBox="0 0 40 40" width="40" height="40" fill="none">
    <circle cx="20" cy="20" r="18" fill="#eff6ff" stroke="#dbeafe" strokeWidth="1.5" />
    <rect x="10" y="14" width="20" height="15" rx="4" fill="#246bfe" />
    <rect x="13" y="17" width="14" height="9" rx="2" fill="#ffffff" />
    <circle cx="17" cy="21" r="2" fill="#246bfe" />
    <circle cx="23" cy="21" r="2" fill="#246bfe" />
    <path d="M18 24h4" stroke="#246bfe" strokeWidth="1.5" strokeLinecap="round" />
    <circle cx="20" cy="11" r="1.5" fill="#246bfe" />
    <line x1="20" y1="12.5" x2="20" y2="14" stroke="#246bfe" strokeWidth="1.5" />
    <rect x="7" y="19" width="3" height="5" rx="1" fill="#246bfe" />
    <rect x="30" y="19" width="3" height="5" rx="1" fill="#246bfe" />
  </svg>
);

const HouseIllustration = ({ className }: { className?: string }) => (
  <svg className={className} viewBox="0 0 160 120" width="160" height="120" fill="none">
    <path d="M20 35a8 8 0 0 1 15-3 6 6 0 0 1 8 8c0 1-.2 2-.5 3H20c0-3 0-5 0-8z" fill="#f0f7ff" opacity="0.8" />
    <path d="M125 45a6 6 0 0 1 11-2 4 4 0 0 1 6 5c0 1-.1 1.5-.3 2H125z" fill="#f0f7ff" opacity="0.8" />
    <path d="M13 55l.5 1.2 1.2.5-1.2.5-.5 1.2-.5-1.2-1.2-.5 1.2-.5z" fill="#93c5fd" />
    <path d="M135 30l.7 1.8 1.8.7-1.8.8-.7 1.8-.8-1.8-1.8-.7 1.8-.8z" fill="#93c5fd" />
    <path d="M142 65l.5 1.2 1.2.5-1.2.5-.5 1.2-.5-1.2-1.2-.5 1.2-.5z" fill="#60a5fa" />
    <path d="M25 78l.5 1.2 1.2.5-1.2.5-.5 1.2-.5-1.2-1.2-.5 1.2-.5z" fill="#60a5fa" />
    <rect x="55" y="55" width="50" height="45" fill="#eff6ff" rx="2" />
    <rect x="55" y="55" width="25" height="45" fill="#e0f2fe" rx="2" />
    <polygon points="50,55 80,30 110,55" fill="#bfdbfe" />
    <polygon points="50,55 80,30 80,55" fill="#93c5fd" />
    <rect x="62" y="65" width="8" height="12" rx="1" fill="#ffffff" />
    <rect x="62" y="65" width="4" height="12" fill="#e0f2fe" />
    <rect x="62" y="70" width="8" height="1" fill="#bfdbfe" />
    <rect x="74" y="65" width="8" height="12" rx="1" fill="#ffffff" />
    <rect x="74" y="65" width="4" height="12" fill="#e0f2fe" />
    <rect x="74" y="70" width="8" height="1" fill="#bfdbfe" />
    <rect x="90" y="65" width="8" height="12" rx="1" fill="#ffffff" />
    <rect x="90" y="65" width="4" height="12" fill="#e0f2fe" />
    <rect x="90" y="70" width="8" height="1" fill="#bfdbfe" />
    <rect x="73" y="85" width="14" height="15" rx="1" fill="#246bfe" opacity="0.8" />
    <circle cx="77" cy="92" r="1" fill="#ffffff" />
    <circle cx="45" cy="85" r="10" fill="#eff6ff" />
    <line x1="45" y1="85" x2="45" y2="100" stroke="#bfdbfe" strokeWidth="2" />
    <circle cx="115" cy="88" r="8" fill="#eff6ff" />
    <line x1="115" y1="88" x2="115" y2="100" stroke="#bfdbfe" strokeWidth="2" />
  </svg>
);

const FilterIcon = ({ className }: { className?: string }) => (
  <svg className={className} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" width="14" height="14">
    <polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3" />
  </svg>
);

const MapPinIcon = ({ className }: { className?: string }) => (
  <svg className={className} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" width="14" height="14">
    <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
    <circle cx="12" cy="10" r="3" />
  </svg>
);

const SmileyIcon = ({ className }: { className?: string }) => (
  <svg className={className} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" width="20" height="20">
    <circle cx="12" cy="12" r="10" />
    <path d="M8 14s1.5 2 4 2 4-2 4-2" />
    <line x1="9" y1="9" x2="9.01" y2="9" />
    <line x1="15" y1="9" x2="15.01" y2="9" />
  </svg>
);

const ClipIcon = ({ className }: { className?: string }) => (
  <svg className={className} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" width="20" height="20">
    <path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48" />
  </svg>
);
