import { useState, useRef } from "react";
import {
  Download,
  Upload,
  RefreshCw,
  HardDrive,
  AlertTriangle,
  CheckCircle,
  Clock,
  FolderOpen,
} from "lucide-react";
import { Button } from "@/components/common/Button";
import { Loading } from "@/components/common/Loading";
import { toast } from "sonner";
import {
  useCreateBackupMutation,
  useListBackupsQuery,
  useRestoreBackupMutation,
  useDownloadBackupMutation,
  useRestoreFromUploadMutation,
  type BackupInfo,
} from "@/api/backupApi";
import clsx from "clsx";
import { Portal } from "@/components/common/Portal";
import { handleApiError } from "@/utils/errorHandler";

export const BackupPage = () => {
  const { data: backupsData, isLoading, refetch } = useListBackupsQuery();
  const [createBackup, { isLoading: isCreating }] = useCreateBackupMutation();
  const [restoreBackup, { isLoading: isRestoring }] =
    useRestoreBackupMutation();
  const [downloadBackup] = useDownloadBackupMutation();
  const [restoreFromUpload, { isLoading: isUploadRestoring }] =
    useRestoreFromUploadMutation();

  const [selectedBackup, setSelectedBackup] = useState<string | null>(null);
  const [showConfirmRestore, setShowConfirmRestore] = useState(false);
  const [showRestoreSuccess, setShowRestoreSuccess] = useState(false);
  const [downloadingFile, setDownloadingFile] = useState<string | null>(null);
  const [restoreDetails, setRestoreDetails] = useState<{
    migrationsApplied: number;
    requiresRestart: boolean;
    dataValidationIssuesFound: number;
  } | null>(null);

  // Upload / Import state
  const [uploadedFile, setUploadedFile] = useState<File | null>(null);
  const [showConfirmUploadRestore, setShowConfirmUploadRestore] =
    useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const backups = backupsData?.data || [];

  const handleCreateBackup = async () => {
    try {
      const result = await createBackup().unwrap();
      if (!result.data) {
        toast.error(result.message || "فشل إنشاء النسخة الاحتياطية");
        return;
      }

      toast.success("تم إنشاء النسخة الاحتياطية بنجاح");
      refetch();
    } catch (error) {
      toast.error(handleApiError(error));
      console.error(error);
    }
  };

  // Download a backup file to the client's computer
  const handleDownloadBackup = async (fileName: string) => {
    try {
      setDownloadingFile(fileName);
      const blob = await downloadBackup(fileName).unwrap();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = fileName;
      document.body.appendChild(anchor);
      anchor.click();
      document.body.removeChild(anchor);
      URL.revokeObjectURL(url);
      toast.success("تم تنزيل الملف بنجاح");
    } catch (error) {
      toast.error("فشل تنزيل الملف");
      console.error(error);
    } finally {
      setDownloadingFile(null);
    }
  };

  // Handle file selection for upload-restore
  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.name.endsWith(".db")) {
      toast.error("يجب اختيار ملف نسخة احتياطية بامتداد .db");
      if (fileInputRef.current) fileInputRef.current.value = "";
      return;
    }
    setUploadedFile(file);
  };

  const handleOpenFilePicker = () => {
    fileInputRef.current?.click();
  };

  // Trigger restore from uploaded file after confirmation
  const handleRestoreFromUpload = async () => {
    if (!uploadedFile) return;

    const formData = new FormData();
    formData.append("file", uploadedFile);

    try {
      const response = await restoreFromUpload(formData).unwrap();
      const result = response.data;

      if (!result) {
        toast.error(response.message || "فشلت عملية الاستعادة");
        setShowConfirmUploadRestore(false);
        return;
      }

      setShowConfirmUploadRestore(false);
      setUploadedFile(null);
      if (fileInputRef.current) fileInputRef.current.value = "";

      setRestoreDetails({
        migrationsApplied: result.migrationsApplied || 0,
        requiresRestart: result.requiresRestart ?? true,
        dataValidationIssuesFound: result.dataValidationIssuesFound || 0,
      });
      setShowRestoreSuccess(true);

      if (result.migrationsApplied > 0) {
        toast.success(
          `تم استعادة الملف المرفوع بنجاح وتطبيق ${result.migrationsApplied} تحديث على قاعدة البيانات`,
        );
      } else {
        toast.success("تم استعادة الملف المرفوع بنجاح");
      }
      refetch();
    } catch (error) {
      toast.error(handleApiError(error));
      console.error(error);
      setShowConfirmUploadRestore(false);
    }
  };

  const handleRestoreBackup = async () => {
    if (!selectedBackup) {
      toast.error("يرجى اختيار نسخة احتياطية");
      return;
    }

    try {
      const response = await restoreBackup({
        backupFileName: selectedBackup,
      }).unwrap();
      const result = response.data;

      if (!result) {
        toast.error(response.message || "فشلت عملية الاستعادة");
        setShowConfirmRestore(false);
        return;
      }

      setShowConfirmRestore(false);
      setSelectedBackup(null);

      // Show detailed success modal with migration & restart info
      setRestoreDetails({
        migrationsApplied: result.migrationsApplied || 0,
        requiresRestart: result.requiresRestart ?? true,
        dataValidationIssuesFound: result.dataValidationIssuesFound || 0,
      });
      setShowRestoreSuccess(true);

      if (result.migrationsApplied > 0) {
        toast.success(
          `تم استعادة النسخة الاحتياطية بنجاح وتطبيق ${result.migrationsApplied} تحديث على قاعدة البيانات`,
        );
      } else {
        toast.success("تم استعادة النسخة الاحتياطية بنجاح");
      }

      refetch();
    } catch (error) {
      toast.error(handleApiError(error));
      console.error(error);
      setShowConfirmRestore(false);
    }
  };

  const formatFileSize = (bytes: number): string => {
    const mb = bytes / (1024 * 1024);
    return mb.toFixed(2) + " MB";
  };

  const formatDate = (dateString: string): string => {
    const date = new Date(dateString);
    return date.toLocaleString("ar-EG");
  };

  const getReasonBadgeColor = (reason: string) => {
    switch (reason) {
      case "pre-migration":
        return "bg-blue-100 text-blue-800";
      case "pre-restore":
        return "bg-purple-100 text-purple-800";
      case "daily-scheduled":
        return "bg-green-100 text-green-800";
      default:
        return "bg-gray-100 text-gray-800";
    }
  };

  const getReasonLabel = (reason: string): string => {
    switch (reason) {
      case "pre-migration":
        return "قبل الترقية";
      case "pre-restore":
        return "قبل الاستعادة";
      case "daily-scheduled":
        return "نسخة يومية";
      default:
        return "يدوية";
    }
  };

  if (isLoading) {
    return <Loading />;
  }

  return (
    <div dir="rtl" className="min-h-screen bg-gray-50 p-4 md:p-6">
      <div className="max-w-7xl mx-auto space-y-5">
        {/* Header */}
        <div className="bg-white border border-gray-200 rounded-2xl p-5 md:p-6">
          <div className="flex items-start gap-3">
            <div className="w-11 h-11 rounded-xl bg-blue-50 border border-blue-100 flex items-center justify-center flex-shrink-0">
              <HardDrive className="w-6 h-6 text-blue-700" />
            </div>
            <div>
              <h1 className="text-2xl md:text-3xl font-bold text-gray-900 mb-1">
                إدارة النسخ الاحتياطية
              </h1>
              <p className="text-sm md:text-base text-gray-600 leading-7">
                قم بإنشاء النسخ الاحتياطية أو استعادة نسخة محفوظة مع عرض حالة
                العملية بشكل واضح.
              </p>
            </div>
          </div>
        </div>

        {/* Action Buttons */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-4">
          <div className="bg-white border border-gray-200 rounded-2xl p-4 shadow-sm min-h-[152px] flex flex-col justify-between lg:col-span-4">
            <p className="text-sm font-semibold text-gray-800 mb-3 flex items-center gap-2">
              <Download className="w-4 h-4 text-blue-600" />
              إنشاء نسخة جديدة
            </p>
            <p className="text-xs text-gray-500 mb-3">
              أنشئ نسخة احتياطية فورية من قاعدة البيانات الحالية.
            </p>
            <Button
              onClick={handleCreateBackup}
              disabled={isCreating}
              className="w-full flex items-center justify-center gap-2 bg-blue-600 hover:bg-blue-700 text-white h-11 rounded-lg"
            >
              {isCreating ? (
                <>
                  <RefreshCw className="w-5 h-5 animate-spin" />
                  جاري الإنشاء...
                </>
              ) : (
                <>
                  <Download className="w-5 h-5" />
                  إنشاء نسخة احتياطية الآن
                </>
              )}
            </Button>
          </div>

          <div className="bg-white border border-gray-200 rounded-2xl p-4 shadow-sm min-h-[152px] lg:col-span-5">
            <p className="text-sm font-semibold text-gray-800 mb-3 flex items-center gap-2">
              <FolderOpen className="w-4 h-4 text-indigo-600" />
              استيراد نسخة احتياطية من جهازك
            </p>
            <p className="text-xs text-gray-500 mb-3">
              اختر ملف بصيغة .db ثم أكمل الاستعادة بعد التأكيد.
            </p>

            <input
              ref={fileInputRef}
              type="file"
              accept=".db"
              onChange={handleFileChange}
              className="hidden"
            />

            <div className="grid grid-cols-1 sm:grid-cols-[minmax(0,1fr)_auto_auto] gap-2 items-center">
              <div
                className="h-10 px-3 border border-gray-200 rounded-lg bg-gray-50 text-xs text-gray-700 flex items-center truncate"
                title={
                  uploadedFile ? uploadedFile.name : "لم يتم اختيار ملف بعد"
                }
              >
                {uploadedFile
                  ? `${uploadedFile.name} (${(uploadedFile.size / 1024 / 1024).toFixed(2)} MB)`
                  : "لم يتم اختيار ملف بعد"}
              </div>

              <button
                onClick={handleOpenFilePicker}
                className="h-10 px-3 border border-indigo-200 text-indigo-700 rounded-lg hover:bg-indigo-50 transition text-sm whitespace-nowrap"
              >
                اختيار ملف
              </button>

              <button
                onClick={() => {
                  if (!uploadedFile) {
                    toast.error("يرجى اختيار ملف أولاً");
                    return;
                  }
                  setShowConfirmUploadRestore(true);
                }}
                disabled={!uploadedFile || isUploadRestoring}
                className="h-10 px-3 bg-indigo-600 text-white text-sm rounded-lg hover:bg-indigo-700 disabled:opacity-50 flex items-center justify-center gap-1 whitespace-nowrap"
              >
                <Upload className="w-4 h-4" />
                استعادة
              </button>
            </div>
          </div>

          <div className="bg-yellow-50 border border-yellow-200 rounded-2xl p-4 shadow-sm min-h-[152px] flex items-center gap-3 lg:col-span-3">
            <AlertTriangle className="w-5 h-5 text-yellow-600 flex-shrink-0" />
            <div className="text-sm text-yellow-800 leading-6">
              <p className="font-semibold">تنبيه مهم:</p>
              <p>تأكد من عمل نسخة احتياطية منتظمة يومياً</p>
            </div>
          </div>
        </div>

        {/* Backups List */}
        <div className="bg-white rounded-2xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="px-6 py-4 border-b border-gray-200">
            <h2 className="text-xl font-semibold text-gray-900 flex items-center gap-2">
              <Clock className="w-5 h-5" />
              قائمة النسخ الاحتياطية
            </h2>
            <p className="text-sm text-gray-600 mt-1">
              إجمالي النسخ:{" "}
              <span className="font-semibold">{backups.length}</span>
            </p>
          </div>

          {backups.length === 0 ? (
            <div className="p-8 text-center">
              <HardDrive className="w-12 h-12 text-gray-300 mx-auto mb-3" />
              <p className="text-gray-500">لا توجد نسخ احتياطية حالياً</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[900px] text-sm">
                <thead className="bg-gray-100 border-b border-gray-200">
                  <tr>
                    <th className="px-6 py-3 text-right text-sm font-semibold text-gray-700">
                      اسم الملف
                    </th>
                    <th className="px-6 py-3 text-right text-sm font-semibold text-gray-700">
                      الحجم
                    </th>
                    <th className="px-6 py-3 text-right text-sm font-semibold text-gray-700">
                      التاريخ والوقت
                    </th>
                    <th className="px-6 py-3 text-right text-sm font-semibold text-gray-700">
                      النوع
                    </th>
                    <th className="px-6 py-3 text-right text-sm font-semibold text-gray-700">
                      الإجراءات
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {backups.map((backup: BackupInfo, index: number) => (
                    <tr
                      key={backup.fileName || index}
                      className={clsx(
                        "border-b border-gray-200 hover:bg-gray-50 transition-colors",
                        selectedBackup === backup.fileName && "bg-blue-50/70",
                      )}
                    >
                      <td className="px-6 py-4 text-sm text-gray-900 font-mono max-w-[320px]">
                        <div
                          className="cursor-pointer hover:underline truncate text-left"
                          dir="ltr"
                          title={backup.fileName}
                          onClick={() =>
                            setSelectedBackup(
                              selectedBackup === backup.fileName
                                ? null
                                : backup.fileName,
                            )
                          }
                        >
                          {backup.fileName}
                        </div>
                      </td>
                      <td
                        className="px-6 py-4 text-sm text-gray-600 whitespace-nowrap text-left"
                        dir="ltr"
                      >
                        {formatFileSize(backup.sizeBytes)}
                      </td>
                      <td
                        className="px-6 py-4 text-sm text-gray-600 whitespace-nowrap text-left"
                        dir="ltr"
                      >
                        {formatDate(backup.createdAt)}
                      </td>
                      <td className="px-6 py-4 text-sm">
                        <span
                          className={clsx(
                            "inline-block px-3 py-1 rounded-full text-xs font-semibold",
                            getReasonBadgeColor(backup.reason),
                          )}
                        >
                          {getReasonLabel(backup.reason)}
                        </span>
                      </td>
                      <td className="px-6 py-4 text-sm whitespace-nowrap">
                        <div className="flex items-center gap-2 flex-wrap">
                          <button
                            onClick={() =>
                              handleDownloadBackup(backup.fileName)
                            }
                            disabled={downloadingFile === backup.fileName}
                            className="inline-flex items-center gap-1 px-3 py-1.5 border border-blue-200 text-blue-700 hover:bg-blue-50 rounded-lg transition disabled:opacity-50 min-w-[90px] justify-center"
                            title="تنزيل النسخة إلى جهازك"
                          >
                            {downloadingFile === backup.fileName ? (
                              <RefreshCw className="w-4 h-4 animate-spin" />
                            ) : (
                              <Download className="w-4 h-4" />
                            )}
                            تنزيل
                          </button>
                          <button
                            onClick={() => {
                              setSelectedBackup(backup.fileName);
                              setShowConfirmRestore(true);
                            }}
                            disabled={isRestoring}
                            className="inline-flex items-center gap-1 px-3 py-1.5 border border-green-200 text-green-700 hover:bg-green-50 rounded-lg transition disabled:opacity-50 min-w-[90px] justify-center"
                            title="استعادة من هذه النسخة"
                          >
                            <Upload className="w-4 h-4" />
                            استعادة
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Confirmation Modal - restore from server backup */}
        {showConfirmRestore && (
          <Portal>
            <div className="fixed inset-0 bg-black/50 backdrop-blur-[1px] flex items-center justify-center p-4 z-[100]">
              <div className="bg-white rounded-2xl shadow-xl max-w-md w-full p-6 border border-gray-100 text-right">
                <div className="flex items-center gap-3 mb-4">
                  <AlertTriangle className="w-6 h-6 text-orange-600" />
                  <h3 className="text-lg font-bold text-gray-900">
                    تأكيد الاستعادة
                  </h3>
                </div>

                <p className="text-gray-600 mb-4">
                  هل أنت متأكد من رغبتك في استعادة النسخة الاحتياطية؟
                </p>

                <div className="bg-orange-50 border border-orange-200 rounded-lg p-3 mb-6">
                  <p className="text-sm font-semibold text-orange-900 mb-1">
                    ملف النسخة:
                  </p>
                  <p className="text-xs text-orange-700 font-mono break-all">
                    {selectedBackup}
                  </p>
                  <p className="text-xs text-orange-700 mt-2">
                    ⚠️ سيتم إنشاء نسخة احتياطية من حالة النظام الحالية قبل
                    الاستعادة
                  </p>
                </div>

                <div className="flex gap-3">
                  <button
                    onClick={() => {
                      setShowConfirmRestore(false);
                      setSelectedBackup(null);
                    }}
                    disabled={isRestoring}
                    className="flex-1 px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 transition disabled:opacity-50"
                  >
                    إلغاء
                  </button>
                  <button
                    onClick={handleRestoreBackup}
                    disabled={isRestoring}
                    className="flex-1 px-4 py-2 bg-orange-600 text-white rounded-lg hover:bg-orange-700 transition disabled:opacity-50 flex items-center justify-center gap-2"
                  >
                    {isRestoring ? (
                      <>
                        <RefreshCw className="w-4 h-4 animate-spin" />
                        جاري...
                      </>
                    ) : (
                      <>
                        <CheckCircle className="w-4 h-4" />
                        نعم، استعادة الآن
                      </>
                    )}
                  </button>
                </div>
              </div>
            </div>
          </Portal>
        )}

        {/* Confirmation Modal - restore from uploaded file */}
        {showConfirmUploadRestore && uploadedFile && (
          <Portal>
            <div className="fixed inset-0 bg-black/50 backdrop-blur-[1px] flex items-center justify-center p-4 z-[100]">
              <div className="bg-white rounded-2xl shadow-xl max-w-md w-full p-6 border border-gray-100 text-right">
                <div className="flex items-center gap-3 mb-4">
                  <AlertTriangle className="w-6 h-6 text-indigo-600" />
                  <h3 className="text-lg font-bold text-gray-900">
                    تأكيد الاستعادة من ملف خارجي
                  </h3>
                </div>

                <p className="text-gray-600 mb-4">
                  هل أنت متأكد من رغبتك في استعادة قاعدة البيانات من الملف الذي
                  اخترته؟
                </p>

                <div className="bg-indigo-50 border border-indigo-200 rounded-lg p-3 mb-6">
                  <p className="text-sm font-semibold text-indigo-900 mb-1">
                    الملف المختار:
                  </p>
                  <p className="text-xs text-indigo-700 font-mono break-all">
                    {uploadedFile.name}
                  </p>
                  <p className="text-xs text-indigo-600 mt-1">
                    الحجم: {(uploadedFile.size / 1024 / 1024).toFixed(2)} MB
                  </p>
                  <p className="text-xs text-orange-700 mt-2">
                    ⚠️ سيتم إنشاء نسخة احتياطية من حالة النظام الحالية قبل
                    الاستعادة
                  </p>
                </div>

                <div className="flex gap-3">
                  <button
                    onClick={() => {
                      setShowConfirmUploadRestore(false);
                      setUploadedFile(null);
                      if (fileInputRef.current) fileInputRef.current.value = "";
                    }}
                    disabled={isUploadRestoring}
                    className="flex-1 px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 transition disabled:opacity-50"
                  >
                    إلغاء
                  </button>
                  <button
                    onClick={handleRestoreFromUpload}
                    disabled={isUploadRestoring}
                    className="flex-1 px-4 py-2 bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 transition disabled:opacity-50 flex items-center justify-center gap-2"
                  >
                    {isUploadRestoring ? (
                      <>
                        <RefreshCw className="w-4 h-4 animate-spin" />
                        جاري الاستعادة...
                      </>
                    ) : (
                      <>
                        <CheckCircle className="w-4 h-4" />
                        نعم، استعادة الآن
                      </>
                    )}
                  </button>
                </div>
              </div>
            </div>
          </Portal>
        )}

        {/* Restore Success Modal */}
        {showRestoreSuccess && restoreDetails && (
          <Portal>
            <div className="fixed inset-0 bg-black/50 backdrop-blur-[1px] flex items-center justify-center p-4 z-[100]">
              <div className="bg-white rounded-2xl shadow-xl max-w-md w-full p-6 border border-gray-100 text-right">
                <div className="flex items-center gap-3 mb-4">
                  <CheckCircle className="w-6 h-6 text-green-600" />
                  <h3 className="text-lg font-bold text-gray-900">
                    تمت الاستعادة بنجاح
                  </h3>
                </div>

                <div className="space-y-3 mb-6">
                  {restoreDetails.migrationsApplied > 0 && (
                    <div className="bg-blue-50 border border-blue-200 rounded-lg p-3">
                      <p className="text-sm font-semibold text-blue-900 mb-1">
                        تحديث قاعدة البيانات:
                      </p>
                      <p className="text-sm text-blue-700">
                        تم تطبيق{" "}
                        <span className="font-bold">
                          {restoreDetails.migrationsApplied}
                        </span>{" "}
                        تحديث على الجداول تلقائياً.
                        <br />
                        النسخة الاحتياطية كانت من إصدار أقدم وتم ترقيتها بنجاح.
                      </p>
                    </div>
                  )}

                  {restoreDetails.migrationsApplied === 0 && (
                    <div className="bg-green-50 border border-green-200 rounded-lg p-3">
                      <p className="text-sm text-green-700">
                        النسخة الاحتياطية من نفس إصدار قاعدة البيانات - لا يوجد
                        تحديثات مطلوبة.
                      </p>
                    </div>
                  )}

                  {restoreDetails.dataValidationIssuesFound > 0 && (
                    <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-3">
                      <p className="text-sm font-semibold text-yellow-900 mb-1">
                        ⚠️ تنبيه: تم اكتشاف{" "}
                        {restoreDetails.dataValidationIssuesFound} مشكلة أثناء
                        التحقق
                      </p>
                      <p className="text-sm text-yellow-700">
                        قد تكون هناك بيانات قديمة لم تتطابق مع الإصدار الجديد.
                        <br />
                        تحقق من السجلات للمزيد من التفاصيل.
                      </p>
                    </div>
                  )}

                  {restoreDetails.dataValidationIssuesFound === 0 && (
                    <div className="bg-green-50 border border-green-200 rounded-lg p-3">
                      <p className="text-sm text-green-700">
                        ✓ تم التحقق من سلامة البيانات بنجاح - لا توجد مشاكل
                      </p>
                    </div>
                  )}

                  <div className="bg-orange-50 border border-orange-200 rounded-lg p-3">
                    <p className="text-sm font-semibold text-orange-900 mb-1">
                      ⚠️ يُنصح بإعادة تشغيل التطبيق
                    </p>
                    <p className="text-sm text-orange-700">
                      لضمان عمل التطبيق بشكل صحيح بعد الاستعادة، يُفضل إعادة
                      تشغيل الخدمة.
                    </p>
                  </div>

                  <div className="bg-gray-50 border border-gray-200 rounded-lg p-3">
                    <p className="text-sm text-gray-600">
                      تم إنشاء نسخة احتياطية من حالة النظام قبل الاستعادة (نسخة
                      أمان).
                    </p>
                  </div>
                </div>

                <button
                  onClick={() => {
                    setShowRestoreSuccess(false);
                    setRestoreDetails(null);
                  }}
                  className="w-full px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 transition flex items-center justify-center gap-2"
                >
                  <CheckCircle className="w-4 h-4" />
                  حسناً، فهمت
                </button>
              </div>
            </div>
          </Portal>
        )}

        {/* Info Section */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mt-1">
          <div className="bg-blue-50 border border-blue-200 rounded-xl p-4 min-h-[110px]">
            <h3 className="font-semibold text-blue-900 mb-2 flex items-center gap-2">
              <Download className="w-4 h-4" />
              النسخ التلقائية
            </h3>
            <p className="text-sm text-blue-700">
              تُنشأ تلقائياً يومياً الساعة 2 صباحاً
            </p>
          </div>

          <div className="bg-green-50 border border-green-200 rounded-xl p-4 min-h-[110px]">
            <h3 className="font-semibold text-green-900 mb-2 flex items-center gap-2">
              <CheckCircle className="w-4 h-4" />
              الاحتفاظ
            </h3>
            <p className="text-sm text-green-700">
              آخر 14 نسخة يومية + 4 نسخ أسبوعية
            </p>
          </div>

          <div className="bg-purple-50 border border-purple-200 rounded-xl p-4 min-h-[110px]">
            <h3 className="font-semibold text-purple-900 mb-2 flex items-center gap-2">
              <HardDrive className="w-4 h-4" />
              الأمان
            </h3>
            <p className="text-sm text-purple-700">
              نسخة قبل الاستعادة تُنشأ تلقائياً
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default BackupPage;
