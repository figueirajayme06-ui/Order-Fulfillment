import type { FC } from "react";
import { BrowserRouter, Route, Routes } from "react-router-dom";
import { AuthProvider } from "./contexts/auth/AuthProvider";
import { AppLayout } from "./components/layout/AppLayout";
import { AgreementsPage } from "./pages/agreements/AgreementsPage";
import { AgreementDetailPage } from "./pages/agreements/AgreementDetailPage";
import { TimelinePage } from "./pages/timeline/TimelinePage";
import { AssetsPage } from "./pages/assets/AssetsPage";
import { AssetProfilePage } from "./pages/assets/AssetProfilePage";
import { RingfencePage } from "./pages/ringfence/RingfencePage";
import { AdminPage } from "./pages/admin/AdminPage";
import "./App.css";

export const App: FC = () => (
  <BrowserRouter>
    <AuthProvider>
      <Routes>
        <Route element={<AppLayout />}>
          <Route index element={<></>} />
          <Route path="/agreements" element={<AgreementsPage />} />
          <Route path="/agreements/:headerId" element={<AgreementDetailPage />} />
          <Route path="/agreements/:headerId/timeline" element={<TimelinePage />} />
          <Route path="/assets" element={<AssetsPage />} />
          <Route path="/assets/:assetId" element={<AssetProfilePage />} />
          <Route path="/ringfence" element={<RingfencePage />} />
          <Route path="/admin" element={<AdminPage />} />
        </Route>
      </Routes>
    </AuthProvider>
  </BrowserRouter>
);
