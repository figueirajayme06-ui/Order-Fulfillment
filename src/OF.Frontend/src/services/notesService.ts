import api from "./api";

export interface RecordNote {
  id: number;
  notes: string;
  lastUpdatedBy: string | null;
  lastUpdatedDate: string | null;
}

export async function fetchAssetNotes(assetId: string): Promise<RecordNote[]> {
  const response = await api.get<RecordNote[]>(`/api/notes/assets/${encodeURIComponent(assetId)}`);
  return response.data;
}

export async function createAssetNote(assetId: string, notes: string): Promise<RecordNote> {
  const response = await api.post<RecordNote>(`/api/notes/assets/${encodeURIComponent(assetId)}`, { notes });
  return response.data;
}

export async function saveAssetNote(assetId: string, noteId: number, notes: string): Promise<RecordNote> {
  const response = await api.put<RecordNote>(`/api/notes/assets/${encodeURIComponent(assetId)}/${noteId}`, { notes });
  return response.data;
}

export async function fetchAgreementNotes(headerId: number): Promise<RecordNote[]> {
  const response = await api.get<RecordNote[]>(`/api/notes/agreements/${headerId}`);
  return response.data;
}

export async function createAgreementNote(headerId: number, notes: string): Promise<RecordNote> {
  const response = await api.post<RecordNote>(`/api/notes/agreements/${headerId}`, { notes });
  return response.data;
}

export async function saveAgreementNote(headerId: number, noteId: number, notes: string): Promise<RecordNote> {
  const response = await api.put<RecordNote>(`/api/notes/agreements/${headerId}/${noteId}`, { notes });
  return response.data;
}
