export interface ConnectedPrinterDevice {
  connectionId: string;
  deviceId: string;
  deviceName: string;
  machineName?: string;
  printerName?: string;
  groupName: string;
  connectedAtUtc: string;
}

export interface PrinterStatus {
  primaryGroup: string;
  fallbackGroup: string;
  bridgeAvailable: boolean;
  totalDevicesInScope: number;
  targetDeviceId?: string;
  targetDeviceConnected?: boolean;
  targetDevice?: ConnectedPrinterDevice;
  preferredDeviceConnectionId?: string;
  preferredDevice?: ConnectedPrinterDevice;
  devices: ConnectedPrinterDevice[];
  checkedAtUtc: string;
}
