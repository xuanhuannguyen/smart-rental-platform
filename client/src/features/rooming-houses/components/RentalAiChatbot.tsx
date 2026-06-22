import { useMemo, useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { toAssetUrl } from '../../../shared/api/assets';
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
    'Xung quanh có quán ăn hay chợ nào không?'
  ]
};

export default function RentalAiChatbot({ context, roomingHouseId, title }: RentalAiChatbotProps) {
  const [open, setOpen] = useState(false);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [messages, setMessages] = useState<ChatMessage[]>(() => [
    {
      id: 'welcome',
      role: 'assistant',
      text: getWelcomeMessage(context, title)
    }
  ]);
  const inputRef = useRef<HTMLInputElement | null>(null);

  const quickPrompts = useMemo(() => QUICK_PROMPTS[context], [context]);

  async function submitMessage(message: string) {
    const trimmed = message.trim();
    if (!trimmed || loading) return;

    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      text: trimmed
    };

    setMessages((current) => [...current, userMessage]);
    setInput('');
    setLoading(true);

    try {
      const response = await chatRoomingHouseAssistant({
        message: trimmed,
        context,
        roomingHouseId: roomingHouseId ?? null,
        mode: 'detailed'
      });

      setMessages((current) => [
        ...current,
        {
          id: crypto.randomUUID(),
          role: 'assistant',
          text: response.reply,
          response
        }
      ]);
    } catch (error) {
      setMessages((current) => [
        ...current,
        {
          id: crypto.randomUUID(),
          role: 'assistant',
          text: getApiErrorMessage(error, 'Mình chưa thể gọi chatbot lúc này. Bạn thử lại sau nhé.')
        }
      ]);
    } finally {
      setLoading(false);
      window.setTimeout(() => inputRef.current?.focus(), 0);
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void submitMessage(input);
  }

  return (
    <div className={`rental-ai-chatbot ${open ? 'rental-ai-chatbot--open' : ''}`}>
      {open && (
        <section className="rental-ai-chatbot__panel" aria-label="Chatbot tư vấn tìm trọ">
          <header className="rental-ai-chatbot__header">
            <div>
              <span className="rental-ai-chatbot__eyebrow">AI tư vấn</span>
              <h2>{context === 'detail' ? 'Hỏi về khu trọ này' : 'Tìm trọ thông minh'}</h2>
            </div>
            <button type="button" className="rental-ai-chatbot__close" onClick={() => setOpen(false)} aria-label="Đóng chatbot">
              ×
            </button>
          </header>

          <div className="rental-ai-chatbot__messages">
            {messages.map((message) => (
              <article key={message.id} className={`rental-ai-chatbot__message rental-ai-chatbot__message--${message.role}`}>
                <p>{message.text}</p>
                {message.response && <ChatResponseBlocks response={message.response} />}
              </article>
            ))}
            {loading && (
              <article className="rental-ai-chatbot__message rental-ai-chatbot__message--assistant">
                <p>AI đang đọc dữ liệu khu trọ và soạn câu trả lời...</p>
              </article>
            )}
          </div>

          <div className="rental-ai-chatbot__quick">
            {quickPrompts.map((prompt) => (
              <button key={prompt} type="button" onClick={() => void submitMessage(prompt)} disabled={loading}>
                {prompt}
              </button>
            ))}
          </div>

          <form className="rental-ai-chatbot__form" onSubmit={handleSubmit}>
            <input
              ref={inputRef}
              value={input}
              onChange={(event) => setInput(event.target.value)}
              placeholder={context === 'detail' ? 'Hỏi chính sách, phòng, xung quanh...' : 'Bạn muốn tìm trọ như thế nào?'}
              disabled={loading}
            />
            <button type="submit" disabled={loading || !input.trim()} aria-label="Gửi câu hỏi">
              Gửi
            </button>
          </form>
        </section>
      )}

      <button type="button" className="rental-ai-chatbot__launcher" onClick={() => setOpen((value) => !value)}>
        <span>AI</span>
        <strong>Tư vấn trọ</strong>
      </button>
    </div>
  );
}

function ChatResponseBlocks({ response }: { response: RoomingHouseAiChatResponse }) {
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
            <span key={question}>{question}</span>
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
  const distance = house.distanceKm != null ? ` · ${formatDistance(house.distanceKm)}` : '';
  return `${rooms}${price ? ` · ${price}` : ''}${distance}`;
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
