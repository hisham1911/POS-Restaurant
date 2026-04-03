import type { ApiResponse } from "@/types/api.types";
import type { ApiError } from "@/utils/errorHandler";

/**
 * Extracts typed data from ApiResponse and throws a normalized ApiError
 * when backend returns a success response without a data payload.
 */
export const extractApiData = <T>(
  response: ApiResponse<T>,
  fallbackErrorCode: string,
  fallbackMessage: string,
): T => {
  if (response.data !== undefined && response.data !== null) {
    return response.data;
  }

  const error: ApiError = {
    status: 500,
    data: {
      errorCode: response.errorCode ?? fallbackErrorCode,
      message: response.message ?? fallbackMessage,
    },
  };

  throw error;
};
