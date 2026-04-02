import {
  useGetCurrentShiftQuery,
  useGetShiftsQuery,
  useOpenShiftMutation,
  useCloseShiftMutation,
} from "../api/shiftsApi";
import {
  OpenShiftRequest,
  CloseShiftRequest,
  Shift,
} from "../types/shift.types";
import { toast } from "sonner";
import { ApiError, getApiErrorCode, handleApiError } from "../utils/errorHandler";

export const useShift = () => {
  const { data: shiftData, isLoading, refetch, isFetching } =
    useGetCurrentShiftQuery(undefined, {
      refetchOnMountOrArgChange: true,
    });
  const { data: shiftsData, isLoading: isLoadingShifts } = useGetShiftsQuery();

  const [openMutation, { isLoading: isOpening }] = useOpenShiftMutation();
  const [closeMutation, { isLoading: isClosing }] = useCloseShiftMutation();

  const currentShift = shiftData?.data || null;
  const shifts = shiftsData?.data || [];
  const hasActiveShift = currentShift && !currentShift.isClosed;

  const openShift = async (data: OpenShiftRequest): Promise<Shift | null> => {
    try {
      const result = await openMutation(data).unwrap();

      if (!result.data) {
        toast.error(result.message || "Unable to open shift");
        return null;
      }

      toast.success("Shift opened successfully");
      refetch();
      return result.data;
    } catch (error) {
      const apiError = error as ApiError;
      if (
        !getApiErrorCode(error) &&
        apiError.status !== 400 &&
        apiError.status !== 403 &&
        apiError.status !== 409
      ) {
        toast.error(handleApiError(error));
      }
      return null;
    }
  };

  const closeShift = async (data: CloseShiftRequest): Promise<Shift | null> => {
    try {
      const result = await closeMutation(data).unwrap();

      if (!result.data) {
        toast.error(result.message || "Unable to close shift");
        return null;
      }

      toast.success("Shift closed successfully");
      refetch();
      return result.data;
    } catch (error) {
      const apiError = error as ApiError;
      if (
        !getApiErrorCode(error) &&
        apiError.status !== 400 &&
        apiError.status !== 403 &&
        apiError.status !== 409
      ) {
        toast.error(handleApiError(error));
      }
      refetch();
      return null;
    }
  };

  return {
    currentShift,
    shifts,
    hasActiveShift,
    isLoading,
    isLoadingShifts,
    isFetching,
    refetch,
    openShift,
    closeShift,
    isOpening,
    isClosing,
  };
};
