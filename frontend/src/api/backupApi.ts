import { baseApi } from "./baseApi";
import { ApiResponse } from "../types/api.types";

export interface BackupInfo {
  fileName: string;
  fullPath: string;
  sizeBytes: number;
  createdAt: string;
  reason: string;
  isPreMigration: boolean;
}

export interface BackupResult {
  success: boolean;
  backupPath?: string;
  backupSizeBytes: number;
  backupTimestamp: string;
  reason?: string;
  integrityCheckPassed: boolean;
  errorMessage?: string;
}

export interface RestoreRequest {
  backupFileName: string;
}

export interface RestoreResult {
  success: boolean;
  restoredFromPath?: string;
  preRestoreBackupPath?: string;
  restoreTimestamp: string;
  maintenanceModeEnabled: boolean;
  errorMessage?: string;
  requiresRestart: boolean;
  migrationsApplied: number;
  dataValidationIssuesFound: number;
}

const backupApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // Create manual backup
    createBackup: builder.mutation<ApiResponse<BackupResult>, void>({
      query: () => ({
        url: "/admin/backup",
        method: "POST",
      }),
      invalidatesTags: ["Backup"],
    }),

    // List all backups
    listBackups: builder.query<ApiResponse<BackupInfo[]>, void>({
      query: () => ({
        url: "/admin/backups",
        method: "GET",
      }),
      providesTags: ["Backup"],
    }),

    // Restore from backup (existing file in server backups directory)
    restoreBackup: builder.mutation<ApiResponse<RestoreResult>, RestoreRequest>({
      query: (request) => ({
        url: "/admin/restore",
        method: "POST",
        body: request,
      }),
      invalidatesTags: ["Backup"],
    }),

    // Download a backup file to the client machine
    downloadBackup: builder.mutation<Blob, string>({
      query: (fileName) => ({
        url: `/admin/backup/${encodeURIComponent(fileName)}/download`,
        method: "GET",
        responseHandler: (response) => response.blob(),
      }),
    }),

    // Restore from an uploaded backup file (from client machine)
    restoreFromUpload: builder.mutation<ApiResponse<RestoreResult>, FormData>({
      query: (formData) => ({
        url: "/admin/restore/upload",
        method: "POST",
        body: formData,
        // Do NOT set Content-Type header; browser sets it with boundary for multipart
        formData: true,
      }),
      invalidatesTags: ["Backup"],
    }),
  }),
});

export const {
  useCreateBackupMutation,
  useListBackupsQuery,
  useRestoreBackupMutation,
  useDownloadBackupMutation,
  useRestoreFromUploadMutation,
} = backupApi;
