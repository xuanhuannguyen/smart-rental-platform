import { useId, type ReactNode, type ChangeEvent } from 'react';
import './Tabs.css';

export interface TabItem {
  id: string;
  label: string;
  icon?: ReactNode;
  disabled?: boolean;
  title?: string;
}

interface TabsProps {
  items: TabItem[];
  activeId: string;
  onChange: (id: string) => void;
  className?: string;
  variant?: 'segmented-primary' | 'segmented-secondary';
}

export function Tabs({ items, activeId, onChange, className = '', variant = 'segmented-primary' }: TabsProps) {
  const selectId = useId();

  const handleSelectChange = (e: ChangeEvent<HTMLSelectElement>) => {
    onChange(e.target.value);
  };

  return (
    <div className={`shared-tabs-wrapper ${className}`}>
      <div className="shared-tabs-mobile">
        <label htmlFor={selectId} className="sr-only">Chọn tab</label>
        <select
          id={selectId}
          className="shared-tabs-select"
          value={activeId}
          onChange={handleSelectChange}
        >
          {items.map((item) => (
            <option key={item.id} value={item.id} disabled={item.disabled}>
              {item.label}
            </option>
          ))}
        </select>
      </div>
      
      <ul className={`shared-tabs-desktop ${variant}`}>
        {items.map((item) => (
          <li key={item.id} className="shared-tab-item">
            <button
              type="button"
              className={`shared-tab-btn ${activeId === item.id ? 'active' : ''}`}
              onClick={() => onChange(item.id)}
              disabled={item.disabled}
              title={item.title}
            >
              {item.icon && <span className="tab-icon">{item.icon}</span>}
              {item.label}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
