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
import { extractApiData } from "@/utils/apiResponse";

export const useShift = () => {
  const {
    data: shiftData,
    isLoading,
    refetch,
    isFetching,
  } = useGetCurrentShiftQuery(undefined, {
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
      const response = await openMutation(data).unwrap();
      const shift = extractApiData(
        response,
        "SHIFT_OPEN_EMPTY_RESPONSE",
        "تعذر فتح الوردية",
      );

      toast.success("تم فتح الوردية بنجاح");
      refetch();
      return shift;
    } catch {
      return null;
    }
  };

  const closeShift = async (data: CloseShiftRequest): Promise<Shift | null> => {
    try {
      const response = await closeMutation(data).unwrap();
      const shift = extractApiData(
        response,
        "SHIFT_CLOSE_EMPTY_RESPONSE",
        "تعذر إغلاق الوردية",
      );

      toast.success("تم إغلاق الوردية بنجاح");
      refetch();
      return shift;
    } catch {
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
