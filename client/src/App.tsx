import { Link, Route, Routes } from 'react-router-dom';
import KycSubmitPage from './pages/KycSubmitPage';
import KycStatusPage from './pages/KycStatusPage';

export default function App() {
  return (
    <div className="app-shell">
      <header className="app-header">
        <h1>Smart Rental Platform</h1>
        <nav>
          <Link to="/kyc/submit">Submit KYC</Link>
          <Link to="/kyc/status">KYC Status</Link>
        </nav>
      </header>
      <main className="app-main">
        <Routes>
          <Route path="/" element={<KycSubmitPage />} />
          <Route path="/kyc/submit" element={<KycSubmitPage />} />
          <Route path="/kyc/status" element={<KycStatusPage />} />
        </Routes>
      </main>
    </div>
  );
}
