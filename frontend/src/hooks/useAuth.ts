import { useNavigate } from "react-router-dom";
import { useAppDispatch, useAppSelector } from "../store/hooks";
import {
  setCredentials,
  logout as logoutAction,
  selectCurrentUser,
  selectIsAuthenticated,
  selectIsAdmin,
  selectIsSystemOwner,
} from "../store/slices/authSlice";
import { clearBranch } from "../store/slices/branchSlice";
import { useLoginMutation } from "../api/authApi";
import { LoginRequest } from "../types/auth.types";
import { toast } from "sonner";
import {
  ApiError,
  getApiErrorCode,
  handleApiError,
} from "../utils/errorHandler";
import { baseApi } from "../api/baseApi";
import { extractApiData } from "@/utils/apiResponse";

export const useAuth = () => {
  const navigate = useNavigate();
  const dispatch = useAppDispatch();

  const user = useAppSelector(selectCurrentUser);
  const isAuthenticated = useAppSelector(selectIsAuthenticated);
  const isAdmin = useAppSelector(selectIsAdmin);
  const isSystemOwner = useAppSelector(selectIsSystemOwner);

  const [loginMutation, { isLoading: isLoggingIn }] = useLoginMutation();

  // Login function using RTK Query
  const login = async (credentials: LoginRequest) => {
    try {
      const response = await loginMutation(credentials).unwrap();
      const loginData = extractApiData(
        response,
        "AUTH_LOGIN_EMPTY_RESPONSE",
        "فشل تسجيل الدخول",
      );

      // CRITICAL: Clear persisted branch state from localStorage BEFORE setting new credentials
      // This prevents redux-persist from rehydrating old branch data for the new user
      try {
        localStorage.removeItem("persist:branch");
      } catch (e) {
        // ignore localStorage errors
      }

      // Clear branch state in Redux
      dispatch(clearBranch());

      dispatch(
        setCredentials({
          user: loginData.user,
          token: loginData.accessToken,
        }),
      );
      toast.success("تم تسجيل الدخول بنجاح");
      navigate(
        loginData.user.role === "SystemOwner" ? "/owner/tenants" : "/pos",
      );
    } catch (error: unknown) {
      const apiError = error as ApiError;
      const errorCode = getApiErrorCode(error);
      if (
        !errorCode &&
        apiError.status !== 400 &&
        apiError.status !== 403 &&
        apiError.status !== 409
      ) {
        toast.error(handleApiError(error));
      }
    }
  };

  const logout = () => {
    dispatch(logoutAction());
    dispatch(clearBranch());
    dispatch(baseApi.util.resetApiState());
    navigate("/login");
    toast.success("تم تسجيل الخروج");
  };

  return {
    user,
    isAuthenticated,
    isAdmin,
    isSystemOwner,
    login,
    isLoggingIn,
    logout,
  };
};
